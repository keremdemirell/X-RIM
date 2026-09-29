using System.Collections.Generic;
using NUnit.Framework;
using XRim.Core;
using XRim.Presentation.Playback;
using XRim.Rules;
using XRim.Rules.Events;
using XRim.Simulation;
using XRim.Simulation.Physics;
using XRim.Simulation.Recording;

namespace XRim.Tests.EditMode.Presentation
{
    public sealed class PresentationSanityTests
    {
        private const int StepRateHz = 100;
        private const int FrameCount = 11;
        private const int SeverStep = 3;
        private const int DeathStep = 8;

        [Test]
        public void TimelinePlayer_FiresEventsOnceInOrder_AtPlaybackSpeed()
        {
            var player = new TimelinePlayer();
            var fired = new List<MatchEvent>();
            player.EventReached += fired.Add;
            player.Play(CreateTimeline(out _));

            player.PlaybackSpeed = 0.5f;
            player.Advance(0.05f);
            Assert.That(fired, Is.Empty, "Half speed: 50 ms real = 25 ms of turn, before the sever at 30 ms");

            player.Advance(0.02f);
            Assert.That(fired.Count, Is.EqualTo(1));
            Assert.That(fired[0], Is.InstanceOf<LimbSeveredEvent>());

            player.Advance(10f);
            Assert.That(fired.Count, Is.EqualTo(2));
            Assert.That(fired[1], Is.InstanceOf<FighterDiedEvent>());
            Assert.That(player.IsFinished, Is.True);
        }

        [Test]
        public void TimelinePlayer_ScrubbingBackDoesNotRefire_ReplayingForwardDoes()
        {
            var player = new TimelinePlayer();
            var fired = new List<MatchEvent>();
            player.EventReached += fired.Add;
            player.Play(CreateTimeline(out _));
            player.Advance(10f);

            player.Seek(SimTime.FromMilliseconds(20));
            Assert.That(fired.Count, Is.EqualTo(2));

            player.Seek(SimTime.FromMilliseconds(40));
            Assert.That(fired.Count, Is.EqualTo(3));
        }

        [Test]
        public void TimelinePlayer_ShowsTheLastFrameAtOrBeforeTheTime()
        {
            var player = new TimelinePlayer();
            PoseSnapshot shown = null;
            player.PoseChanged += pose => shown = pose;
            TurnTimeline timeline = CreateTimeline(out SimClock clock);
            player.Play(timeline);

            player.Seek(clock.TimeAtStep(4) + SimTime.FromMilliseconds(5));
            Assert.That(shown, Is.SameAs(timeline.Frames[4].Pose));

            player.StepFrames(-2);
            Assert.That(shown, Is.SameAs(timeline.Frames[2].Pose));
        }

        private static TurnTimeline CreateTimeline(out SimClock clock)
        {
            clock = new SimClock(StepRateHz);
            var recorder = new TimelineRecorder();
            for (int step = 0; step < FrameCount; step++)
            {
                recorder.RecordFrame(step, clock.TimeAtStep(step), new PoseSnapshot());
            }

            recorder.RecordEvent(new LimbSeveredEvent(clock.TimeAtStep(SeverStep), Side.Right, BodyPart.LeftArm));
            recorder.RecordEvent(new FighterDiedEvent(clock.TimeAtStep(DeathStep), Side.Right));
            return recorder.Build();
        }
    }
}
