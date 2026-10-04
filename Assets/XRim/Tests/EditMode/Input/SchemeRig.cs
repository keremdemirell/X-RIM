using System;
using System.Collections.Generic;
using XRim.Core;
using XRim.Input;
using XRim.Networking;
using XRim.Rules;
using XRim.Rules.Match;
using XRim.Rules.Planning;
using XRim.Rules.Settings;
using XRim.Simulation.Drivers;
using XRim.Simulation.Physics;
using XRim.Simulation.Recording;

namespace XRim.Tests.EditMode.Input
{
    /// <summary>Touches handed over one frame at a time, as a script.</summary>
    internal sealed class FakeTouchSource : ITouchSource
    {
        private readonly Queue<TouchSample[]> _frames = new Queue<TouchSample[]>();

        public Vec2 ScreenSizePixels { get; set; }

        public int PendingFrames => _frames.Count;

        public void Frame(params TouchSample[] samples) => _frames.Enqueue(samples);

        public void ReadInto(List<TouchSample> samples)
        {
            if (_frames.Count > 0) samples.AddRange(_frames.Dequeue());
        }
    }

    /// <summary>Remembers every command and answers with <see cref="Result"/>.</summary>
    internal sealed class RecordingSink : IPlanningCommandSink
    {
        public List<PlanningCommand> Commands { get; } = new List<PlanningCommand>();
        public CommandResult Result { get; set; } = CommandResult.Ok;

        public CommandResult Send(PlanningCommand command)
        {
            Commands.Add(command);
            return Result;
        }

        public int Count<T>() where T : PlanningCommand
        {
            int count = 0;
            foreach (PlanningCommand command in Commands)
            {
                if (command is T) count++;
            }

            return count;
        }

        public T Only<T>() where T : PlanningCommand
        {
            T found = null;
            foreach (PlanningCommand command in Commands)
            {
                if (!(command is T match)) continue;
                if (found != null) throw new InvalidOperationException("More than one " + typeof(T).Name + " was sent.");
                found = match;
            }

            return found ?? throw new InvalidOperationException("No " + typeof(T).Name + " was sent.");
        }
    }

    internal sealed class FixedBlocker : ITouchBlocker
    {
        private readonly Vec2 _min;
        private readonly Vec2 _max;

        public FixedBlocker(Vec2 min, Vec2 max)
        {
            _min = min;
            _max = max;
        }

        public bool Blocks(Vec2 p) => p.X >= _min.X && p.X <= _max.X && p.Y >= _min.Y && p.Y <= _max.Y;
    }

    /// <summary>
    /// A dual-zone scheme wired to a scripted touch source, a recording sink and a real planning window, on a 1920×1080
    /// screen showing what the sandbox camera shows. The fighter's frame sits at the turn-start root (-350, 120) for the left
    /// side and (350, 120) for the right.
    /// </summary>
    internal sealed class SchemeRig
    {
        public const float Width = 1920f;
        public const float Height = 1080f;
        public const float BodyZoneFraction = 0.15f;
        public const float SwipeWindowSeconds = 0.3f;
        public const double FrameSeconds = 1.0 / 60.0;

        public static readonly ArenaViewFrame View = new ArenaViewFrame(new Vec2(0f, 525f), 1150f);

        public FakeTouchSource Touches { get; } = new FakeTouchSource();
        public RecordingSink Sink { get; } = new RecordingSink();
        public ScreenLayout Layout { get; }
        public Side Side { get; }
        public BodyPose Root { get; }
        public PlanningWindow Window { get; }
        public DualZoneInputScheme Scheme { get; }

        public SchemeRig(bool mirrored = false, Side side = Side.Left, bool tapClearsMove = true, ITouchBlocker blocker = null)
        {
            Side = side;
            Layout = new ScreenLayout(Width, Height, BodyZoneFraction, mirrored, side);
            Touches.ScreenSizePixels = new Vec2(Width, Height);
            Root = new BodyPose(new Vec2(side == Side.Left ? -350f : 350f, 120f), 0f);

            RulesSettings settings = GddStartingValues.CreateRulesSettings();
            var pose = new PoseSnapshot();
            pose.Left.Set(BodyPart.Torso, new BodyPose(new Vec2(-350f, 120f), 0f));
            pose.Right.Set(BodyPart.Torso, new BodyPose(new Vec2(350f, 120f), 0f));
            var board = new BoardSnapshot(MatchState.CreateInitial(TestData.CreateMatchSetup(), settings), pose);
            Window = new PlanningWindow(0, settings.Match.PlanningDurationSeconds,
                PerSide<PlanningConstraints>.Create(_ => PlanningConstraints.CreateDefault(settings.Match)), false, board,
                PerSide<IWeaponTipSource>.Create(_ => new Rules.Planning.FixedWeaponTipSource(new Vec2(100f, 100f))));

            var swipe = new SwipeSettings(SwipeWindowSeconds, 0.03f, 30f);
            Scheme = new DualZoneInputScheme(Touches, new FourWaySwipeClassifier(swipe), swipe,
                new DualZoneSettings(SwipeWindowSeconds, 0.01f, tapClearsMove), new ScreenToArenaMapper(), () => View, blocker);
            Scheme.Begin(Window, Layout, Sink);
        }

        /// <summary>Hands the scheme one frame of touches and lets it read them.</summary>
        public void Tick(params TouchSample[] samples)
        {
            Touches.Frame(samples);
            Scheme.Tick();
        }

        /// <summary>Where on the screen a point of the fighter's frame is drawn: the inverse of the mapper.</summary>
        public Vec2 PixelOf(Vec2 torsoLocal, ScreenLayout layout = null)
        {
            layout = layout ?? Layout;
            Vec2 arena = TorsoFrame.ToArena(torsoLocal, Root, Side);
            float shownX = layout.ArenaViewFlipped ? -arena.X : arena.X;
            float pixelsPerUnit = layout.ScreenHeightPixels / View.VisibleHeightUnits;
            return new Vec2(layout.ScreenWidthPixels * 0.5f + (shownX - View.CenterUnits.X) * pixelsPerUnit,
                layout.ScreenHeightPixels * 0.5f + (arena.Y - View.CenterUnits.Y) * pixelsPerUnit);
        }

        /// <summary>The torso-frame point a screen pixel should become.</summary>
        public Vec2 LocalOf(Vec2 pixel, ScreenLayout layout = null) =>
            new ScreenToArenaMapper().ToFighterFrame(pixel, layout ?? Layout, View, Root, Side);

        public static TouchSample Down(int finger, Vec2 p, double t) => new TouchSample(finger, TouchSamplePhase.Began, p, t);

        public static TouchSample Move(int finger, Vec2 p, double t) => new TouchSample(finger, TouchSamplePhase.Moved, p, t);

        public static TouchSample Up(int finger, Vec2 p, double t) => new TouchSample(finger, TouchSamplePhase.Ended, p, t);

        public static TouchSample Cancel(int finger, Vec2 p, double t) => new TouchSample(finger, TouchSamplePhase.Canceled, p, t);

        /// <summary>
        /// A finger drawing <paramref name="shape"/> (a torso-frame curve over 0..1) the way a phone reports it: readings at
        /// <paramref name="sampleHz"/>, the finger speeding up and slowing down, each position rounded to whole pixels.
        /// </summary>
        public List<TouchSample> Stroke(int finger, Func<float, Vec2> shape, float seconds, float sampleHz, double startSeconds = 0.0,
            ScreenLayout layout = null)
        {
            int steps = (int)Math.Round(seconds * sampleHz);
            var samples = new List<TouchSample>(steps + 1);
            for (int i = 0; i <= steps; i++)
            {
                float time = (float)i / steps;
                float progress = time * time * (3f - 2f * time); // ease in and out: uneven finger speed
                Vec2 pixel = PixelOf(shape(progress), layout);
                pixel = new Vec2((float)Math.Round(pixel.X), (float)Math.Round(pixel.Y));
                TouchSamplePhase phase = i == 0 ? TouchSamplePhase.Began : i == steps ? TouchSamplePhase.Ended : TouchSamplePhase.Moved;
                samples.Add(new TouchSample(finger, phase, pixel, startSeconds + i / (double)sampleHz));
            }

            return samples;
        }

        /// <summary>Plays readings into the scheme, grouped into frames of <paramref name="frameHz"/>.</summary>
        public void Play(List<TouchSample> samples, float frameHz)
        {
            int index = 0;
            while (index < samples.Count)
            {
                long frame = (long)Math.Floor(samples[index].TimeSeconds * frameHz + 1e-9);
                var batch = new List<TouchSample>();
                while (index < samples.Count && (long)Math.Floor(samples[index].TimeSeconds * frameHz + 1e-9) == frame)
                {
                    batch.Add(samples[index++]);
                }

                Tick(batch.ToArray());
            }
        }
    }
}
