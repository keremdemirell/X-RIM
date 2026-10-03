using System;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using XRim.Config;
using XRim.Core;
using XRim.Rules;
using XRim.Rules.Arena;
using XRim.Rules.Events;
using XRim.Rules.Match;
using XRim.Rules.Paths;
using XRim.Rules.Planning;
using XRim.Rules.Settings;
using XRim.Simulation;
using XRim.Simulation.Drivers;
using XRim.Simulation.Execution;
using XRim.Simulation.Physics;
using XRim.Simulation.Recording;
using XRim.Simulation.Settings;
using XRim.Simulation.Unity2D;

namespace XRim.DebugTools.Diagnostics
{
    /// <summary>
    /// The feel-tuning report (first written for PT1's D1 and D2 decisions, Session 02), measured on the real turn loop with
    /// the live tuning: standing stability, tunnelling of the thin rapier at speed, kinematic vs motor behaviour, and how
    /// long a turn takes to simulate. Each scenario is a fixed one-sided swing (an attacker en garde against an unarmed
    /// target), so runs can be compared after tuning. It runs in its own physics world, never the match's.
    /// </summary>
    internal sealed class FeelDiagnostics
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

        /// <summary>Where the target stands: its chest at this share of the weapon's reach (arm + weapon), so it can be hit.</summary>
        private const float TargetReachFraction = 0.85f;

        private const Handedness ScenarioHandedness = Handedness.Right;

        private static readonly float[] SpeedMultipliers = { 1f, 2f, 4f, 8f };
        private static readonly RagdollSegmentation[] Segmentations = { RagdollSegmentation.SixBodies, RagdollSegmentation.TenBodies };
        private static readonly WeaponDriverKind[] Drivers = { WeaponDriverKind.Kinematic, WeaponDriverKind.Motor };
        private static readonly WeaponId[] Weapons = { WeaponIds.Rapier, WeaponIds.Mace };

        private readonly Unity2DPhysicsWorld _world;
        private readonly IWeaponAimModel _aim = new AimFromShoulderModel();
        private readonly RulePolicies _policies = new RulePolicies();
        private readonly StringBuilder _text = new StringBuilder();

        public FeelDiagnostics(Unity2DPhysicsWorld world)
        {
            _world = Guard.NotNull(world, nameof(world));
        }

        private enum Shape
        {
            FreeThrust,
            ThrustIntoChest,
            Chop,
        }

        public string Run(RulesSettings rules, SimulationSettings simulation)
        {
            _text.Clear();
            Line("X-RIM feel diagnostics, {0:yyyy-MM-dd HH:mm}", DateTime.Now);
            Line("Live tuning: {0} Hz, gravity {1:0} units/s², driver {2}, {3} bodies, motor {4:0.#} Hz (damping {5:0.##}), " +
                 "root drive {6}, guard {7:0}°, target at {8:0.##} of reach.",
                simulation.StepRateHz, simulation.GravityUnitsPerSecondSquared, simulation.WeaponDriver, (int)simulation.Segmentation,
                simulation.WeaponMotor.FrequencyHz, simulation.WeaponMotor.DampingRatio, simulation.RootDrive.Mode,
                simulation.Ragdoll.GuardAngleDegrees, TargetReachFraction);

            Standing(rules, simulation);
            Tunnelling(rules, simulation);
            CompareDrivers(rules, simulation);
            Timing(rules, simulation);
            return _text.ToString();
        }

        private void Standing(RulesSettings rules, SimulationSettings simulation)
        {
            Section("1. Standing still for 2 s, no input (attacker holds the rapier en garde, target unarmed)");
            WeaponStats rapier = rules.FindWeapon(WeaponIds.Rapier);
            if (rapier == null)
            {
                Line("   skipped: the tuning profile has no rapier.");
                return;
            }

            ArenaEdges edges = _policies.ArenaEdge.EdgesFor(rules.Arena);
            foreach (RagdollSegmentation segmentation in Segmentations)
            {
                SimulationSettings sim = With(simulation, s => s.Segmentation = segmentation);
                PoseSnapshot board = CreateBoard(sim, rules.Paths, rapier, DistanceInsideReach(rules.Paths, sim.Ragdoll, rapier));
                _world.Load(board, CreateState(rules, WeaponIds.Rapier), rules, sim, edges);

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

        private void Tunnelling(RulesSettings rules, SimulationSettings simulation)
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
                    float distance = DistanceInsideReach(fast.Paths, sim.Ragdoll, rapier);
                    TurnContact thrust = FirstHit(Swing(fast, sim, rapier, distance, Shape.ThrustIntoChest, out _, out _));
                    TurnContact chop = FirstHit(Swing(fast, sim, rapier, distance, Shape.Chop, out _, out _));
                    Line("   {0,3} Hz  ×{1,-2}  {2,6:0.0} units per step   thrust: {3,-28} chop: {4}", rate, multiplier,
                        rapier.SpeedUnitsPerSecond / rate, HitText(thrust), HitText(chop));
                }
            }
        }

        private void CompareDrivers(RulesSettings rules, SimulationSettings simulation)
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
                    TurnResult free = Swing(rules, sim, weapon, FarDistanceUnits, Shape.FreeThrust, out PoseSnapshot freeBoard, out WeaponPath freePath);
                    Tracking freeTracking = Track(free, freeBoard, freePath, weapon, SimTime.Zero);
                    double pathSeconds = freePath.LengthUnits / weapon.SpeedUnitsPerSecond;
                    double settleAfter = free.Timeline.Duration.Seconds - pathSeconds;

                    float distance = DistanceInsideReach(rules.Paths, sim.Ragdoll, weapon);
                    TurnResult hit = Swing(rules, sim, weapon, distance, Shape.ThrustIntoChest, out PoseSnapshot hitBoard, out WeaponPath hitPath);
                    TurnContact first = FirstHit(hit);
                    Tracking afterHit = Track(hit, hitBoard, hitPath, weapon, first != null ? first.Time : hit.Timeline.Duration);

                    Line("   {0,-6} {1,-9} free thrust: lag max {2:0.0}, end error {3:0.0} units, still {4:0.000} s after the path ({5}).",
                        weapon.Id, driver, freeTracking.MaxLagUnits, freeTracking.EndErrorUnits, settleAfter, free.EndReason);
                    Line("   {0,-6} {1,-9} into the chest: {2}; knockback {3:0.0} units, tilt {4:0.0}°; deflection {5:0.0} units ({6}).",
                        weapon.Id, driver, HitText(first), afterHit.TargetPushUnits, afterHit.TargetTiltDegrees, afterHit.MaxLagUnits,
                        hit.EndReason);
                }
            }
        }

        private void Timing(RulesSettings rules, SimulationSettings simulation)
        {
            Section($"4. Time to simulate one turn in the Editor (rapier thrust into the chest, mean of {TimingRuns}; includes loading both dummies)");
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
                    float distance = DistanceInsideReach(rules.Paths, sim.Ragdoll, rapier);
                    double total = 0.0;
                    double worst = 0.0;
                    int steps = 0;
                    for (int run = 0; run < TimingRuns; run++)
                    {
                        Stopwatch watch = Stopwatch.StartNew();
                        TurnResult result = Swing(rules, sim, rapier, distance, Shape.ThrustIntoChest, out _, out _);
                        watch.Stop();
                        total += watch.Elapsed.TotalMilliseconds;
                        worst = Math.Max(worst, watch.Elapsed.TotalMilliseconds);
                        steps = StepsSimulated(result);
                    }

                    Line("   {0,2} bodies, {1,-9}: {2,6:0.00} ms mean, {3,6:0.00} ms worst, {4} steps per turn.", (int)segmentation, driver,
                        total / TimingRuns, worst, steps);
                }
            }
        }

        /// <summary>One turn on the scenario board: the left dummy swings along a scenario path, the right one stands unarmed.</summary>
        private TurnResult Swing(RulesSettings rules, SimulationSettings simulation, WeaponStats weapon, float distance, Shape shape,
            out PoseSnapshot board, out WeaponPath path)
        {
            board = CreateBoard(simulation, rules.Paths, weapon, distance);
            Vec2 guardTip = TorsoFrame.ToLocal(Tip(board.Left.HeldItem, weapon), TurnStartRoot.Of(board.Left), Side.Left);
            path = PathFor(shape, guardTip, rules.Paths, simulation.Ragdoll, weapon, distance);
            var plans = new PerSide<TurnPlan>(new TurnPlan(weapon.WeaponId, BodyMove.None, path, default, true), TurnPlan.Empty(weapon.WeaponId));
            var input = new TurnInput(new BoardSnapshot(CreateState(rules, weapon.WeaponId), board), plans, rules, simulation);
            // Raw physics: the report measures the drivers and the ragdoll, so no hit rule stops or slows the weapon.
            var options = new TurnSimulatorOptions { Aim = _aim, Contacts = new RecordContactsHandler() };
            return new TurnSimulator(_world, _policies, options).Simulate(input);
        }

        /// <summary>The attacker on the left in the guard stance and an unarmed target on the right, mirrored around x = 0.</summary>
        private PoseSnapshot CreateBoard(SimulationSettings simulation, PathSettings paths, WeaponStats attackerWeapon, float distanceUnits)
        {
            float pelvisHeight = simulation.Ragdoll.LegLengthUnits;
            BodyPart arm = BodyParts.DominantArm(ScenarioHandedness);
            return new PoseSnapshot
            {
                Left = GuardStance.Create(new BodyPose(new Vec2(-distanceUnits * 0.5f, pelvisHeight), 0f), Side.Left, arm, attackerWeapon,
                    simulation.Segmentation, simulation.Ragdoll, paths, _aim),
                Right = GuardStance.Create(new BodyPose(new Vec2(distanceUnits * 0.5f, pelvisHeight), 0f), Side.Right, arm, null,
                    simulation.Segmentation, simulation.Ragdoll, paths, _aim),
            };
        }

        private static MatchState CreateState(RulesSettings rules, WeaponId weapon) => new MatchState(
            PerSide<FighterState>.Create(_ => new FighterState(ScenarioHandedness, rules.Damage.MaxHp, weapon)),
            PerSide<ElectricWallState>.Create(_ => new ElectricWallState()));

        /// <summary>A pelvis-to-pelvis distance that puts the target's chest at a share of the weapon's reach (GDD §6).</summary>
        private static float DistanceInsideReach(PathSettings paths, RagdollSettings body, WeaponStats weapon) =>
            paths.ShoulderOffsetUnits.X + (paths.ArmLengthUnits + weapon.LengthUnits) * TargetReachFraction + body.TorsoWidthUnits * 0.5f;

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
                    // Straight toward the opponent at the guard tip's height, as far as the reach allows.
                    Vec2 offset = guardTip - shoulder;
                    double room = reach * reach - offset.Y * offset.Y;
                    float length = room > 0.0 ? Math.Max(0f, Math.Min(FreeThrustUnits, (float)Math.Sqrt(room) - offset.X)) : 0f;
                    return new WeaponPath(new[] { guardTip, guardTip + Vec2.UnitX * length });
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
        private static Tracking Track(TurnResult result, PoseSnapshot board, WeaponPath path, WeaponStats weapon, SimTime from)
        {
            var cursor = new PathCursor(path);
            BodyPose root = TurnStartRoot.Of(board.Left);
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
                Vec2 scheduled = TorsoFrame.ToArena(cursor.PointAt((float)(frame.Time.Seconds * weapon.SpeedUnitsPerSecond)), root, Side.Left);
                tracking.MaxLagUnits = Math.Max(tracking.MaxLagUnits, Vec2.Distance(Tip(frame.Pose.Left.HeldItem, weapon), scheduled));
            }

            FighterPose last = result.FinalBoard.Pose.Left;
            Vec2 end = TorsoFrame.ToArena(cursor.PointAt(cursor.LengthUnits), root, Side.Left);
            tracking.EndErrorUnits = last.HasHeldItem ? Vec2.Distance(Tip(last.HeldItem, weapon), end) : float.NaN;
            return tracking;
        }

        /// <summary>The first contact of the attacker's weapon with the target's body.</summary>
        private static TurnContact FirstHit(TurnResult result)
        {
            foreach (MatchEvent matchEvent in result.Timeline.Events)
            {
                if (!(matchEvent is ContactEvent contactEvent)) continue;
                TurnContact contact = contactEvent.Contact;
                if (contact.WeaponSide != Side.Left) continue;
                BodyTag other = contact.Facts.A.Role == BodyRole.HeldItem && contact.Facts.A.Owner == Side.Left ? contact.Facts.B : contact.Facts.A;
                if (other.Owner == Side.Right && other.Role == BodyRole.BodyPart) return contact;
            }

            return null;
        }

        private static string HitText(TurnContact hit)
        {
            if (hit == null) return "MISSED";
            BodyTag other = hit.Facts.A.Role == BodyRole.HeldItem ? hit.Facts.B : hit.Facts.A;
            return string.Format(CultureInfo.InvariantCulture, "hit {0} at {1:0.0} ms (d {2:0})", other.Part, hit.Time.Milliseconds,
                hit.PathDistanceUnits);
        }

        private static int StepsSimulated(TurnResult result) =>
            result.Timeline.Frames.Count > 0 ? result.Timeline.Frames[result.Timeline.Frames.Count - 1].Step : 0;

        private static Vec2 Tip(BodyPose grip, WeaponStats weapon) =>
            grip.PositionUnits + Vec2.FromAngleDegrees(grip.RotationDegrees) * weapon.LengthUnits;

        private static SimulationSettings With(SimulationSettings settings, Action<SimulationSettings> change)
        {
            SimulationSettings copy = SettingsCopy.Clone(settings);
            change(copy);
            return copy;
        }

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
