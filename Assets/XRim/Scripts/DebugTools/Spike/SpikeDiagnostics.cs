using System;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using XRim.Config;
using XRim.Core;
using XRim.Rules;
using XRim.Rules.Paths;
using XRim.Rules.Settings;
using XRim.Simulation;
using XRim.Simulation.Drivers;
using XRim.Simulation.Execution;
using XRim.Simulation.Physics;
using XRim.Simulation.Recording;
using XRim.Simulation.Settings;
using XRim.Simulation.Unity2D;

namespace XRim.DebugTools.Spike
{
    /// <summary>
    /// The PT1 evidence for D1 (weapon driver) and D2 (segmentation), measured in the hidden physics world with the live
    /// tuning: standing stability, tunnelling of the thin rapier at speed, kinematic vs motor behaviour, and how long a
    /// swing takes to simulate. Every scenario is a fixed swing on the spike board, so runs can be compared after tuning.
    /// </summary>
    internal sealed class SpikeDiagnostics
    {
        private const double StandingSeconds = 2.0;
        private const int TimingRuns = 10;
        private const float FreeThrustUnits = 150f;
        private const float FarDistanceUnits = 1200f;
        private const float RunUpUnits = 100f;
        private const float OvershootUnits = 60f;
        private const float ReachMarginUnits = 10f;
        private const float FloorClearanceUnits = 10f;
        private const int SlowStepRateHz = 60;

        private static readonly float[] SpeedMultipliers = { 1f, 2f, 4f, 8f };
        private static readonly RagdollSegmentation[] Segmentations = { RagdollSegmentation.SixBodies, RagdollSegmentation.TenBodies };
        private static readonly WeaponDriverKind[] Drivers = { WeaponDriverKind.Kinematic, WeaponDriverKind.Motor };
        private static readonly WeaponId[] Weapons = { WeaponIds.Rapier, WeaponIds.Mace };

        private readonly Unity2DPhysicsWorld _world;
        private readonly Ragdoll _sixBodies;
        private readonly Ragdoll _tenBodies;
        private readonly ArenaSpace _space;
        private readonly IWeaponAimModel _aim;
        private readonly StringBuilder _text = new StringBuilder();

        public SpikeDiagnostics(Unity2DPhysicsWorld world, Ragdoll sixBodies, Ragdoll tenBodies, ArenaSpace space, IWeaponAimModel aim)
        {
            _world = Guard.NotNull(world, nameof(world));
            _sixBodies = sixBodies;
            _tenBodies = tenBodies;
            _space = space;
            _aim = Guard.NotNull(aim, nameof(aim));
        }

        private enum Shape
        {
            FreeThrust,
            ThrustIntoChest,
            Chop,
        }

        public string Run(RulesSettings rules, SimulationSettings simulation, float reachFraction)
        {
            _text.Clear();
            Line("X-RIM feel spike diagnostics, {0:yyyy-MM-dd HH:mm}", DateTime.Now);
            Line("Live tuning: {0} Hz, gravity {1:0} units/s², driver {2}, {3} bodies, motor {4:0.#} Hz (damping {5:0.##}), " +
                 "root drive {6}, target at {7:0.##} of reach.",
                simulation.StepRateHz, simulation.GravityUnitsPerSecondSquared, simulation.WeaponDriver, (int)simulation.Segmentation,
                simulation.WeaponMotor.FrequencyHz, simulation.WeaponMotor.DampingRatio, simulation.RootDrive.Mode, reachFraction);

            Standing(rules, simulation, reachFraction);
            Tunnelling(rules, simulation, reachFraction);
            CompareDrivers(rules, simulation, reachFraction);
            Timing(rules, simulation, reachFraction);
            return _text.ToString();
        }

        private void Standing(RulesSettings rules, SimulationSettings simulation, float reachFraction)
        {
            Section("1. Standing still for 2 s, no input (attacker holds the rapier en garde, target unarmed)");
            WeaponStats rapier = rules.FindWeapon(WeaponIds.Rapier);
            if (rapier == null)
            {
                Line("   skipped: the tuning profile has no rapier.");
                return;
            }

            foreach (RagdollSegmentation segmentation in Segmentations)
            {
                SimulationSettings sim = With(simulation, s => s.Segmentation = segmentation);
                float distance = SpikeBoard.DistanceInsideReach(rules.Paths, sim.Ragdoll, rapier, reachFraction);
                PoseSnapshot board = SpikeBoard.CreatePose(Template(segmentation), sim, _space, _aim, rapier, distance);
                _world.Load(board, SpikeBoard.CreateState(rules, WeaponIds.Rapier), rules, sim);

                var clock = new SimClock(sim.StepRateHz);
                int steps = clock.StepsFor(StandingSeconds);
                int settledRun = 0;
                double settledAt = -1.0;
                float maxDrift = 0f;
                float maxTilt = 0f;
                for (int i = 0; i < steps; i++)
                {
                    _world.Step(clock.StepSeconds);
                    bool settled = _world.IsSettled(sim.SettleLinearSpeedUnitsPerSecond, sim.SettleAngularSpeedDegreesPerSecond);
                    settledRun = settled ? settledRun + 1 : 0;
                    if (settledRun == sim.SettleStepsRequired && settledAt < 0.0) settledAt = (double)(i + 1) / sim.StepRateHz;
                    foreach (Side side in new[] { Side.Left, Side.Right })
                    {
                        BodyPose start = board.Get(side).Get(BodyPart.Torso);
                        BodyPose now = _world.GetPose(side, BodyPart.Torso);
                        maxDrift = Math.Max(maxDrift, Vec2.Distance(start.PositionUnits, now.PositionUnits));
                        maxTilt = Math.Max(maxTilt, Math.Abs(XMath.DeltaAngleDegrees(start.RotationDegrees, now.RotationDegrees)));
                    }
                }

                string settledText = settledAt < 0.0
                    ? "NEVER settled"
                    : $"settled after {settledAt:0.000} s" + (settledRun >= sim.SettleStepsRequired ? " and stayed settled" : " but moved again");
                Line("   {0} bodies: {1}; max pelvis drift {2:0.0} units; max torso tilt {3:0.0}°.", (int)segmentation, settledText, maxDrift,
                    maxTilt);
            }
        }

        private void Tunnelling(RulesSettings rules, SimulationSettings simulation, float reachFraction)
        {
            Section("2. Tunnelling: does the thin rapier still register at speed? (kinematic driver; hit = any contact with the target's body)");
            WeaponStats baseRapier = rules.FindWeapon(WeaponIds.Rapier);
            if (baseRapier == null)
            {
                Line("   skipped: the tuning profile has no rapier.");
                return;
            }

            Line("   Rapier {0:0} wide, {1:0} units/s. Thrust = straight into the chest at shoulder height; chop = straight down " +
                 "through the head and chest.", baseRapier.InkThicknessUnits, baseRapier.SpeedUnitsPerSecond);
            foreach (int rate in new[] { simulation.StepRateHz, SlowStepRateHz })
            {
                foreach (float multiplier in SpeedMultipliers)
                {
                    RulesSettings fast = SettingsCopy.Clone(rules);
                    WeaponStats rapier = fast.FindWeapon(WeaponIds.Rapier);
                    rapier.SpeedUnitsPerSecond *= multiplier;
                    SimulationSettings sim = With(simulation, s =>
                    {
                        s.StepRateHz = rate;
                        s.WeaponDriver = WeaponDriverKind.Kinematic;
                    });
                    float distance = SpikeBoard.DistanceInsideReach(fast.Paths, sim.Ragdoll, rapier, reachFraction);
                    SwingContact thrust = FirstHit(Swing(fast, sim, rapier, distance, Shape.ThrustIntoChest, out _, out _));
                    SwingContact chop = FirstHit(Swing(fast, sim, rapier, distance, Shape.Chop, out _, out _));
                    Line("   {0,3} Hz  ×{1,-2}  {2,6:0.0} units per step   thrust: {3,-28} chop: {4}", rate, multiplier,
                        rapier.SpeedUnitsPerSecond / rate, HitText(thrust), HitText(chop));
                }
            }
        }

        private void CompareDrivers(RulesSettings rules, SimulationSettings simulation, float reachFraction)
        {
            Section($"3. Kinematic vs motor ({(int)simulation.Segmentation} bodies)");
            Line("   Free thrust: no target in reach; lag = how far the blade tip trails the d = v·t point while moving.");
            Line("   Into the chest: knockback = how far the target's pelvis is pushed; deflection = how far the tip leaves its path after the hit.");
            foreach (WeaponId weaponId in Weapons)
            {
                WeaponStats weapon = rules.FindWeapon(weaponId);
                if (weapon == null) continue;
                foreach (WeaponDriverKind driver in Drivers)
                {
                    SimulationSettings sim = With(simulation, s => s.WeaponDriver = driver);
                    SwingResult free = Swing(rules, sim, weapon, FarDistanceUnits, Shape.FreeThrust, out PoseSnapshot freeBoard, out WeaponPath freePath);
                    Tracking freeTracking = Track(free, freeBoard, freePath, weapon, SimTime.Zero);
                    double pathSeconds = freePath.LengthUnits / weapon.SpeedUnitsPerSecond;
                    double settleAfter = free.Timeline.Duration.Seconds - pathSeconds;

                    float distance = SpikeBoard.DistanceInsideReach(rules.Paths, sim.Ragdoll, weapon, reachFraction);
                    SwingResult hit = Swing(rules, sim, weapon, distance, Shape.ThrustIntoChest, out PoseSnapshot hitBoard, out WeaponPath hitPath);
                    SwingContact first = FirstHit(hit);
                    Tracking afterHit = Track(hit, hitBoard, hitPath, weapon, first != null ? first.Time : hit.Timeline.Duration);

                    Line("   {0,-6} {1,-9} free thrust: lag max {2:0.0}, end error {3:0.0} units, still {4:0.000} s after the path ({5}).",
                        weapon.Id, driver, freeTracking.MaxLagUnits, freeTracking.EndErrorUnits, settleAfter, free.EndReason);
                    Line("   {0,-6} {1,-9} into the chest: {2}; knockback {3:0.0} units, tilt {4:0.0}°; deflection {5:0.0} units ({6}).",
                        weapon.Id, driver, HitText(first), afterHit.TargetPushUnits, afterHit.TargetTiltDegrees, afterHit.MaxLagUnits,
                        hit.EndReason);
                }
            }
        }

        private void Timing(RulesSettings rules, SimulationSettings simulation, float reachFraction)
        {
            Section($"4. Time to simulate one swing in the Editor (rapier thrust into the chest, mean of {TimingRuns}; includes loading both dummies)");
            WeaponStats rapier = rules.FindWeapon(WeaponIds.Rapier);
            if (rapier == null) return;
            foreach (RagdollSegmentation segmentation in Segmentations)
            {
                foreach (WeaponDriverKind driver in Drivers)
                {
                    SimulationSettings sim = With(simulation, s =>
                    {
                        s.Segmentation = segmentation;
                        s.WeaponDriver = driver;
                    });
                    float distance = SpikeBoard.DistanceInsideReach(rules.Paths, sim.Ragdoll, rapier, reachFraction);
                    double total = 0.0;
                    double worst = 0.0;
                    int steps = 0;
                    for (int run = 0; run < TimingRuns; run++)
                    {
                        Stopwatch watch = Stopwatch.StartNew();
                        SwingResult result = Swing(rules, sim, rapier, distance, Shape.ThrustIntoChest, out _, out _);
                        watch.Stop();
                        total += watch.Elapsed.TotalMilliseconds;
                        worst = Math.Max(worst, watch.Elapsed.TotalMilliseconds);
                        steps = result.StepsSimulated;
                    }

                    Line("   {0,2} bodies, {1,-9}: {2,6:0.00} ms mean, {3,6:0.00} ms worst, {4} steps per swing.", (int)segmentation, driver,
                        total / TimingRuns, worst, steps);
                }
            }
        }

        private SwingResult Swing(RulesSettings rules, SimulationSettings simulation, WeaponStats weapon, float distance, Shape shape,
            out PoseSnapshot board, out WeaponPath path)
        {
            board = SpikeBoard.CreatePose(Template(simulation.Segmentation), simulation, _space, _aim, weapon, distance);
            BodyPose torso = board.Left.Get(BodyPart.Torso);
            Vec2 guardTip = TorsoFrame.ToLocal(Tip(board.Left.HeldItem, weapon), torso, Side.Left);
            path = PathFor(shape, guardTip, rules.Paths, simulation.Ragdoll, weapon, distance);
            var input = new SwingInput(board, SpikeBoard.CreateState(rules, weapon.WeaponId), new PerSide<WeaponPath>(path, null), rules,
                simulation);
            return new SwingSimulator(_world, _aim).Run(input);
        }

        /// <summary>Scenario paths in the attacker's torso frame, all starting at the guard tip (D3) and kept within reach (D4).</summary>
        private static WeaponPath PathFor(Shape shape, Vec2 guardTip, PathSettings paths, RagdollSettings body, WeaponStats weapon,
            float distance)
        {
            Vec2 shoulder = paths.ShoulderOffsetUnits;
            float reach = paths.ArmLengthUnits + weapon.LengthUnits - ReachMarginUnits;
            switch (shape)
            {
                case Shape.FreeThrust:
                {
                    Vec2 outward = (guardTip - shoulder).Normalized;
                    float length = Math.Max(0f, Math.Min(FreeThrustUnits, reach - Vec2.Distance(guardTip, shoulder)));
                    return new WeaponPath(new[] { guardTip, guardTip + outward * length });
                }
                case Shape.ThrustIntoChest:
                {
                    float face = distance - body.TorsoWidthUnits * 0.5f;
                    float end = Math.Min(face + OvershootUnits, shoulder.X + reach);
                    return new WeaponPath(new[] { guardTip, new Vec2(face - RunUpUnits, shoulder.Y), new Vec2(end, shoulder.Y) });
                }
                default:
                {
                    // Just inside the target's chest and head, straight down from as high as the reach allows.
                    float x = distance - body.TorsoWidthUnits * 0.25f;
                    float half = (float)Math.Sqrt(Math.Max(0.0, reach * reach - (x - shoulder.X) * (x - shoulder.X)));
                    float bottom = Math.Max(shoulder.Y - half, FloorClearanceUnits - body.LegLengthUnits);
                    return new WeaponPath(new[] { guardTip, new Vec2(x, shoulder.Y + half), new Vec2(x, bottom) });
                }
            }
        }

        /// <summary>How the actual blade tip followed the scheduled d = v·t point, and how the target moved, from a time on.</summary>
        private static Tracking Track(SwingResult result, PoseSnapshot board, WeaponPath path, WeaponStats weapon, SimTime from)
        {
            var cursor = new PathCursor(path);
            BodyPose torso = board.Left.Get(BodyPart.Torso);
            BodyPose target = board.Right.Get(BodyPart.Torso);
            double pathSeconds = path.LengthUnits / weapon.SpeedUnitsPerSecond;
            var tracking = new Tracking();
            foreach (TimelineFrame frame in result.Timeline.Frames)
            {
                BodyPose targetNow = frame.Pose.Right.Get(BodyPart.Torso);
                tracking.TargetPushUnits = Math.Max(tracking.TargetPushUnits, Vec2.Distance(target.PositionUnits, targetNow.PositionUnits));
                tracking.TargetTiltDegrees = Math.Max(tracking.TargetTiltDegrees,
                    Math.Abs(XMath.DeltaAngleDegrees(target.RotationDegrees, targetNow.RotationDegrees)));
                if (frame.Time < from || frame.Time.Seconds > pathSeconds || !frame.Pose.Left.HasHeldItem) continue;
                Vec2 scheduled = TorsoFrame.ToArena(cursor.PointAt((float)(frame.Time.Seconds * weapon.SpeedUnitsPerSecond)), torso, Side.Left);
                tracking.MaxLagUnits = Math.Max(tracking.MaxLagUnits, Vec2.Distance(Tip(frame.Pose.Left.HeldItem, weapon), scheduled));
            }

            FighterPose last = result.FinalPose.Left;
            Vec2 end = TorsoFrame.ToArena(cursor.PointAt(cursor.LengthUnits), torso, Side.Left);
            tracking.EndErrorUnits = last.HasHeldItem ? Vec2.Distance(Tip(last.HeldItem, weapon), end) : float.NaN;
            return tracking;
        }

        private static SwingContact FirstHit(SwingResult result)
        {
            foreach (SwingContact contact in result.Contacts)
            {
                if (contact.WeaponSide != Side.Left) continue;
                BodyTag other = contact.Facts.A.Role == BodyRole.HeldItem && contact.Facts.A.Owner == Side.Left ? contact.Facts.B : contact.Facts.A;
                if (other.Owner == Side.Right && other.Role == BodyRole.BodyPart) return contact;
            }

            return null;
        }

        private static string HitText(SwingContact hit)
        {
            if (hit == null) return "MISSED";
            BodyTag other = hit.Facts.A.Role == BodyRole.HeldItem ? hit.Facts.B : hit.Facts.A;
            return string.Format(CultureInfo.InvariantCulture, "hit {0} at {1:0.0} ms (d {2:0})", other.Part, hit.Time.Milliseconds,
                hit.PathDistanceUnits);
        }

        private static Vec2 Tip(BodyPose grip, WeaponStats weapon) =>
            grip.PositionUnits + Vec2.FromAngleDegrees(grip.RotationDegrees) * weapon.LengthUnits;

        private static SimulationSettings With(SimulationSettings settings, Action<SimulationSettings> change)
        {
            SimulationSettings copy = SettingsCopy.Clone(settings);
            change(copy);
            return copy;
        }

        private Ragdoll Template(RagdollSegmentation segmentation) =>
            segmentation == RagdollSegmentation.TenBodies ? _tenBodies : _sixBodies;

        private void Section(string title)
        {
            _text.AppendLine();
            _text.AppendLine(title);
        }

        private void Line(string format, params object[] args) => _text.AppendLine(string.Format(CultureInfo.InvariantCulture, format, args));

        private sealed class Tracking
        {
            public float MaxLagUnits;
            public float EndErrorUnits;
            public float TargetPushUnits;
            public float TargetTiltDegrees;
        }
    }
}
