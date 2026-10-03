using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using XRim.Config;
using XRim.Core;
using XRim.Rules;
using XRim.Rules.Arena;
using XRim.Rules.Combat;
using XRim.Rules.Events;
using XRim.Rules.Match;
using XRim.Rules.Paths;
using XRim.Rules.Planning;
using XRim.Rules.Settings;
using XRim.Rules.Status;
using XRim.Simulation;
using XRim.Simulation.Drivers;
using XRim.Simulation.Execution;
using XRim.Simulation.Physics;
using XRim.Simulation.Recording;
using XRim.Simulation.Settings;
using XRim.Simulation.Unity2D;
using Object = UnityEngine.Object;

namespace XRim.Tests.PlayMode.Simulation
{
    /// <summary>
    /// Clashes and shield blocks on real Unity 2D physics (Session 07; GDD §7, §10). Both dummies are right-handed and mirrored
    /// around x = 0. Heights in the torso frame (pelvis = 0): shoulder 100, torso 0–120 and 60 wide, head centre 145. Each
    /// scenario sets the guards and paths so the two held items meet before anything touches a body, at an angle well inside
    /// the hard (≥ 30°) or glancing range, and logs "[XRim clashes] …" with the angle, the powers and what happened.
    /// </summary>
    public sealed class ClashPlayModeTests
    {
        private const float Tolerance = 1e-3f;
        private const float LevelGuardDegrees = 0f;
        private const float RaisedGuardDegrees = 6f;
        private const float LoweredGuardDegrees = -6f;

        /// <summary>Pelvis to pelvis 700: level guards leave the rapier's and the mace's tips 80 apart, on one line.</summary>
        private const float HeadOnHalfGapUnits = 350f;

        /// <summary>Pelvis to pelvis 900: two rapiers crossing in the middle stay clear of each other's arms.</summary>
        private const float SlideHalfGapUnits = 450f;

        /// <summary>Pelvis to pelvis 640: a level rapier thrust ends 30 inside the opponent's chest.</summary>
        private const float ShieldHalfGapUnits = 320f;

        /// <summary>A level thrust at shoulder height, 200 from a level guard.</summary>
        private static readonly Vec2 RapierThrustEndLocal = new Vec2(600f, 100f);

        /// <summary>A shorter thrust that still comes down onto a rising mace, ending well short of the mace holder's hand.</summary>
        private static readonly Vec2 RapierShortThrustEndLocal = new Vec2(560f, 100f);

        /// <summary>The mace's level thrust, 200 from its level guard.</summary>
        private static readonly Vec2 MaceThrustEndLocal = new Vec2(420f, 100f);

        /// <summary>From the low guard, up to shoulder height first, then level into the chest (it reaches the face plane late).</summary>
        private static readonly Vec2 RapierRiseLocal = new Vec2(450f, 100f);
        private static readonly Vec2 RapierChestEndLocal = new Vec2(640f, 100f);

        /// <summary>The shield's arc: forward and up from its low guard, ending face out in front of the upper chest.</summary>
        private static readonly Vec2 ShieldArcMiddleLocal = new Vec2(150f, 50f);
        private static readonly Vec2 ShieldRaisedLocal = new Vec2(110f, 100f);

        /// <summary>A long harmless swing up above the dummy's own head, so the turn lasts while the other side holds its shield.</summary>
        private static readonly Vec2 HighSwingMiddleLocal = new Vec2(300f, 200f);
        private static readonly Vec2 HighSwingEndLocal = new Vec2(100f, 300f);

        private SimulationMode2D _previousMode;
        private ArenaSpace _space;
        private RulesSettings _rules;
        private SimulationSettings _simulation;
        private RagdollSettings _body;
        private WeaponStats _rapier;
        private WeaponStats _mace;
        private WeaponStats _shield;
        private RulePolicies _policies;
        private Ragdoll _sixBodies;
        private Ragdoll _tenBodies;
        private readonly AimFromShoulderModel _aim = new AimFromShoulderModel();
        private readonly List<Unity2DPhysicsWorld> _worlds = new List<Unity2DPhysicsWorld>();

        [SetUp]
        public void SetUp()
        {
            _previousMode = Physics2D.simulationMode;
            Physics2D.simulationMode = SimulationMode2D.Script;
            _space = new ArenaSpace(ArenaSpaceConfig.DefaultWorldUnitsPerArenaUnit);
            _rules = GddStartingValues.CreateRulesSettings();
            _simulation = new SimulationSettings();
            _body = _simulation.Ragdoll;
            _rapier = _rules.FindWeapon(WeaponIds.Rapier);
            _mace = _rules.FindWeapon(WeaponIds.Mace);
            _shield = _rules.FindWeapon(WeaponIds.Shield);
            _policies = new RulePolicies();
            _sixBodies = BuildTemplate(RagdollSegmentation.SixBodies);
            _tenBodies = BuildTemplate(RagdollSegmentation.TenBodies);
            _worlds.Clear();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (Unity2DPhysicsWorld world in _worlds)
            {
                Scene scene = world.Scene;
                world.Dispose();
                yield return new WaitUntil(() => !scene.isLoaded);
            }

            Object.DestroyImmediate(_sixBodies.gameObject);
            Object.DestroyImmediate(_tenBodies.gameObject);
            Physics2D.simulationMode = _previousMode;
        }

        private Ragdoll BuildTemplate(RagdollSegmentation segmentation)
        {
            Ragdoll ragdoll = PlaceholderRagdollBuilder.Build(new RagdollBuildSpec(_body, _rules.Paths, segmentation, _space, _rules.Weapons, null));
            ragdoll.gameObject.SetActive(false);
            return ragdoll;
        }

        // --- Boards, plans and turns ------------------------------------------------------------

        private BodyPose TorsoAt(float x) => new BodyPose(new Vec2(x, _body.LegLengthUnits), 0f);

        /// <summary>A dummy en garde with an item, at the default guard or another angle (A3: a shield rests face out).</summary>
        private FighterPose Fighter(Side side, float pelvisX, WeaponStats item, float? guardAngleDegrees = null)
        {
            RagdollSettings body = _body;
            if (guardAngleDegrees.HasValue) body = new RagdollSettings { GuardAngleDegrees = guardAngleDegrees.Value };
            return GuardStance.Create(TorsoAt(pelvisX), side, BodyPart.RightArm, item, _simulation.Segmentation, body, _rules.Paths, _aim);
        }

        private MatchState State(WeaponStats left, WeaponStats right) => new MatchState(
            new PerSide<FighterState>(
                new FighterState(Handedness.Right, _rules.Damage.MaxHp, left.WeaponId),
                new FighterState(Handedness.Right, _rules.Damage.MaxHp, right.WeaponId)),
            PerSide<ElectricWallState>.Create(_ => new ElectricWallState()));

        private static TurnPlan Plan(WeaponStats item, WeaponPath path) =>
            new TurnPlan(item.WeaponId, BodyMove.None, path ?? WeaponPath.Empty, default, true);

        private TurnResult Simulate(PoseSnapshot board, WeaponStats left, WeaponStats right, WeaponPath leftPath, WeaponPath rightPath)
        {
            var world = new Unity2DPhysicsWorld(_space, new RagdollPrefabSet(_sixBodies, _tenBodies));
            _worlds.Add(world);
            var plans = new PerSide<TurnPlan>(Plan(left, leftPath), Plan(right, rightPath));
            return new TurnSimulator(world, _policies).Simulate(new TurnInput(new BoardSnapshot(State(left, right), board), plans, _rules, _simulation));
        }

        /// <summary>The point of a held item that follows its path (a weapon's tip, the shield's centre), in the arena.</summary>
        private static Vec2 PathPoint(BodyPose grip, WeaponStats item) => HeldItemShape.Of(item).PathPointAt(grip);

        private static Vec2 PathPointLocal(PoseSnapshot board, Side side, WeaponStats item) =>
            TorsoFrame.ToLocal(PathPoint(board.Get(side).HeldItem, item), TurnStartRoot.Of(board.Get(side)), side);

        /// <summary>A path from where the item rests (the D3 lead-in start) through the given torso-frame points.</summary>
        private static WeaponPath Path(PoseSnapshot board, Side side, WeaponStats item, params Vec2[] pointsLocal)
        {
            var points = new List<Vec2> { PathPointLocal(board, side, item) };
            points.AddRange(pointsLocal);
            return new WeaponPath(points);
        }

        private static Vec2 ToArena(PoseSnapshot board, Side side, Vec2 local) => TorsoFrame.ToArena(local, TurnStartRoot.Of(board.Get(side)), side);

        private static List<T> EventsOf<T>(TurnResult result) where T : MatchEvent => result.Timeline.Events.OfType<T>().ToList();

        private static string DescribeHits(TurnResult result)
        {
            List<HitLandedEvent> hits = EventsOf<HitLandedEvent>(result);
            if (hits.Count == 0) return "no hits";
            return string.Join("; ", hits.Select(hit => string.Format(CultureInfo.InvariantCulture, "{0} → {1} at {2:0.0} ms",
                hit.Hit.Attacker, hit.Hit.Part, hit.Time.Milliseconds)));
        }

        private static string DescribeClash(WeaponClashEvent clash) => string.Format(CultureInfo.InvariantCulture,
            "{0} at {1:0.0} ms, {2:0.0}°, powers L {3:0.##} / R {4:0.##}, winner {5}", clash.Result.Kind, clash.Time.Milliseconds,
            clash.ContactAngleDegrees, clash.Result.LeftPower, clash.Result.RightPower, clash.Result.Winner?.ToString() ?? "none");

        private static bool IsStaggered(TurnResult result, Side side) =>
            result.Report.ResolvedState.Fighters[side].Statuses.Any(status => status.Kind == StatusKind.Staggered);

        private static void Report(string format, params object[] args) =>
            Debug.Log("[XRim clashes] " + string.Format(CultureInfo.InvariantCulture, format, args));

        // --- Clashes (§10) ---------------------------------------------------------------------

        /// <summary>
        /// Hard clash, ratio ≥ 1.5: two level thrusts meet tip to tip, square on. The mace (10 + 0.005 × 250 = 11.25) crushes the
        /// rapier (2 + 0.005 × 900 = 6.5): the rapier is knocked off its path and its dummy staggered; the mace carries on.
        /// </summary>
        [UnityTest]
        public IEnumerator AMaceCrushesARapier_KnockingItOffAndStaggeringItsDummy()
        {
            var board = new PoseSnapshot
            {
                Left = Fighter(Side.Left, -HeadOnHalfGapUnits, _rapier, LevelGuardDegrees),
                Right = Fighter(Side.Right, HeadOnHalfGapUnits, _mace, LevelGuardDegrees),
            };
            WeaponPath thrust = Path(board, Side.Left, _rapier, RapierThrustEndLocal);
            WeaponPath maceThrust = Path(board, Side.Right, _mace, MaceThrustEndLocal);

            TurnResult result = Simulate(board, _rapier, _mace, thrust, maceThrust);

            List<WeaponClashEvent> clashes = EventsOf<WeaponClashEvent>(result);
            Vec2 rapierTip = PathPoint(result.FinalBoard.Pose.Left.HeldItem, _rapier);
            Vec2 maceTip = PathPoint(result.FinalBoard.Pose.Right.HeldItem, _mace);
            Report("Mace vs rapier: {0}. Rapier tip ends at x {1:0} (its path ends at {2:0}); mace tip at x {3:0} (its path ends at {4:0}). {5}",
                clashes.Count == 1 ? DescribeClash(clashes[0]) : clashes.Count + " clashes", rapierTip.X,
                ToArena(board, Side.Left, RapierThrustEndLocal).X, maceTip.X, ToArena(board, Side.Right, MaceThrustEndLocal).X, DescribeHits(result));

            Assert.That(clashes, Has.Count.EqualTo(1), "the tips meet once (D19)");
            WeaponClashEvent clash = clashes[0];
            Assert.That(clash.Result.IsHardClash, Is.True, "square on: " + DescribeClash(clash));
            Assert.That(clash.Result.Kind, Is.EqualTo(ClashKind.CrushThrough), DescribeClash(clash));
            Assert.That(clash.Result.Winner, Is.EqualTo(Side.Right), "the mace");
            Assert.That(IsStaggered(result, Side.Left), Is.True, "the crushed rapier's dummy is staggered (D17)");
            Assert.That(IsStaggered(result, Side.Right), Is.False);
            Assert.That(rapierTip.X, Is.LessThan(clash.ContactPointUnits.X + 20f), "the rapier is knocked off its path, not carrying on");
            Assert.That(maceTip.X, Is.LessThan(clash.ContactPointUnits.X - 100f), "the mace carries on from the contact point");
            Assert.That(EventsOf<HitLandedEvent>(result), Is.Empty, DescribeHits(result));
            yield return null;
        }

        /// <summary>
        /// Glancing, similar mass: one rapier raised a little and one lowered a little thrust past each other, converging until
        /// their blades meet side by side with their motion along the blades. Both slide past and run their whole paths.
        /// </summary>
        [UnityTest]
        public IEnumerator TwoRapiers_GlancingSideBySide_SlidePastAndBothRunTheirPaths()
        {
            var board = new PoseSnapshot
            {
                Left = Fighter(Side.Left, -SlideHalfGapUnits, _rapier, RaisedGuardDegrees),
                Right = Fighter(Side.Right, SlideHalfGapUnits, _rapier, LoweredGuardDegrees),
            };
            WeaponPath left = Path(board, Side.Left, _rapier, RapierThrustEndLocal);
            WeaponPath right = Path(board, Side.Right, _rapier, RapierThrustEndLocal);

            TurnResult result = Simulate(board, _rapier, _rapier, left, right);

            List<WeaponClashEvent> clashes = EventsOf<WeaponClashEvent>(result);
            Vec2 leftTip = PathPoint(result.FinalBoard.Pose.Left.HeldItem, _rapier);
            Vec2 rightTip = PathPoint(result.FinalBoard.Pose.Right.HeldItem, _rapier);
            Vec2 leftEnd = ToArena(board, Side.Left, RapierThrustEndLocal);
            Vec2 rightEnd = ToArena(board, Side.Right, RapierThrustEndLocal);
            Report("Two rapiers: {0}. Tips end {1:0.0} and {2:0.0} from their path ends. {3}",
                clashes.Count == 1 ? DescribeClash(clashes[0]) : clashes.Count + " clashes", Vec2.Distance(leftTip, leftEnd),
                Vec2.Distance(rightTip, rightEnd), DescribeHits(result));

            Assert.That(clashes, Has.Count.EqualTo(1), "the blades meet once (D19)");
            Assert.That(clashes[0].Result.IsHardClash, Is.False, "glancing: " + DescribeClash(clashes[0]));
            Assert.That(clashes[0].Result.Kind, Is.EqualTo(ClashKind.BothSlidePast), DescribeClash(clashes[0]));
            Assert.That(Vec2.Distance(leftTip, leftEnd), Is.LessThan(2f), "the left rapier runs its whole path");
            Assert.That(Vec2.Distance(rightTip, rightEnd), Is.LessThan(2f), "the right rapier runs its whole path");
            Assert.That(IsStaggered(result, Side.Left) || IsStaggered(result, Side.Right), Is.False);
            Assert.That(EventsOf<HitLandedEvent>(result), Is.Empty, DescribeHits(result));
            yield return null;
        }

        /// <summary>
        /// Glancing, masses far apart: the raised rapier's tip comes down onto the top of the rising mace with their motion
        /// along the blades. Agility wins: the lighter rapier deflects the mace off its path and runs its own.
        /// </summary>
        [UnityTest]
        public IEnumerator AGlancingRapier_DeflectsAMaceOffItsPath()
        {
            var board = new PoseSnapshot
            {
                Left = Fighter(Side.Left, -HeadOnHalfGapUnits, _rapier, RaisedGuardDegrees),
                Right = Fighter(Side.Right, HeadOnHalfGapUnits, _mace, LoweredGuardDegrees),
            };
            WeaponPath thrust = Path(board, Side.Left, _rapier, RapierShortThrustEndLocal);
            WeaponPath maceThrust = Path(board, Side.Right, _mace, MaceThrustEndLocal);

            TurnResult result = Simulate(board, _rapier, _mace, thrust, maceThrust);

            List<WeaponClashEvent> clashes = EventsOf<WeaponClashEvent>(result);
            Vec2 rapierTip = PathPoint(result.FinalBoard.Pose.Left.HeldItem, _rapier);
            Vec2 maceTip = PathPoint(result.FinalBoard.Pose.Right.HeldItem, _mace);
            Vec2 rapierEnd = ToArena(board, Side.Left, RapierShortThrustEndLocal);
            Vec2 maceEnd = ToArena(board, Side.Right, MaceThrustEndLocal);
            Report("Rapier vs mace, glancing: {0}. Rapier tip ends {1:0.0} from its path end; mace tip at x {2:0} (its path ends at {3:0}). {4}",
                clashes.Count == 1 ? DescribeClash(clashes[0]) : clashes.Count + " clashes", Vec2.Distance(rapierTip, rapierEnd), maceTip.X,
                maceEnd.X, DescribeHits(result));

            Assert.That(clashes, Has.Count.EqualTo(1));
            WeaponClashEvent clash = clashes[0];
            Assert.That(clash.Result.IsHardClash, Is.False, "glancing: " + DescribeClash(clash));
            Assert.That(clash.Result.Kind, Is.EqualTo(ClashKind.LighterDeflectsHeavier), DescribeClash(clash));
            Assert.That(clash.Result.Winner, Is.EqualTo(Side.Left), "the lighter rapier");
            Assert.That(Vec2.Distance(rapierTip, rapierEnd), Is.LessThan(2f), "the rapier carries on along its path");
            Assert.That(maceTip.X, Is.GreaterThan(maceEnd.X + 50f), "the mace is redirected off its path and misses");
            Assert.That(IsStaggered(result, Side.Right), Is.False, "a deflection staggers nobody");
            Assert.That(EventsOf<HitLandedEvent>(result), Is.Empty, DescribeHits(result));
            yield return null;
        }

        // --- Shield (§7) ---------------------------------------------------------------------

        /// <summary>
        /// The shield is held like a shield (A3) and protects only where it is: swept up in an arc to stand face out in front of
        /// the chest, it fully blocks a level thrust square on its face (D20). Control: held low at its guard, the same thrust
        /// passes over it into the chest.
        /// </summary>
        [UnityTest]
        public IEnumerator AShieldArc_BlocksAThrust_ThatHitsTheChestWithoutIt()
        {
            var board = new PoseSnapshot
            {
                Left = Fighter(Side.Left, -ShieldHalfGapUnits, _rapier),
                Right = Fighter(Side.Right, ShieldHalfGapUnits, _shield),
            };
            WeaponPath thrust = Path(board, Side.Left, _rapier, RapierRiseLocal, RapierChestEndLocal);
            WeaponPath arc = Path(board, Side.Right, _shield, ShieldArcMiddleLocal, ShieldRaisedLocal);
            double arcSeconds = arc.LengthUnits / _shield.SpeedUnitsPerSecond;

            TurnResult control = Simulate(board, _rapier, _shield, thrust, null);
            TurnResult blocked = Simulate(board, _rapier, _shield, thrust, arc);

            List<ShieldBlockEvent> blocks = EventsOf<ShieldBlockEvent>(blocked);
            BodyPose shield = blocked.FinalBoard.Pose.Right.HeldItem;
            Report("Without the arc: {0}. With the arc ({1:0.000} s): {2}. Shield ends at ({3:0}, {4:0}) facing {5:0}°.",
                DescribeHits(control), arcSeconds,
                blocks.Count == 1
                    ? string.Format(CultureInfo.InvariantCulture, "{0} block at {1:0.0} ms, {2:0.0}° to the face, face position {3:0.00}; {4}",
                        blocks[0].Result.AttackStopped ? "full" : "partial", blocks[0].Time.Milliseconds, blocks[0].ContactAngleDegrees,
                        blocks[0].FacePositionFraction, DescribeHits(blocked))
                    : blocks.Count + " blocks; " + DescribeHits(blocked),
                shield.PositionUnits.X, shield.PositionUnits.Y, shield.RotationDegrees);

            List<HitLandedEvent> controlHits = EventsOf<HitLandedEvent>(control);
            Assert.That(controlHits, Is.Not.Empty, "control: the thrust reaches the dummy");
            Assert.That(controlHits[0].Hit.Part, Is.EqualTo(BodyPart.Torso), "control: over the lowered shield into the chest");
            Assert.That(EventsOf<ShieldBlockEvent>(control), Is.Empty, "control: the lowered shield is not in the way");

            Assert.That(blocks, Has.Count.EqualTo(1), "the raised shield meets the thrust");
            Assert.That(blocks[0].Blocker, Is.EqualTo(Side.Right));
            Assert.That(blocks[0].Result.AttackStopped, Is.True, "square on the face: a full block (D20)");
            Assert.That(blocks[0].Time.Seconds, Is.GreaterThan(arcSeconds), "test setup: the arc is done before the thrust arrives");
            Assert.That(EventsOf<HitLandedEvent>(blocked), Is.Empty, "the blocked thrust lands nothing: " + DescribeHits(blocked));
            Assert.That(blocked.Report.ResolvedState.Fighters[Side.Right].Hp, Is.EqualTo(_rules.Damage.MaxHp));
            yield return null;
        }

        /// <summary>
        /// A tap raises the shield (A4: a short lead-in to the tapped spot, D3) and it holds there, face out, for the whole
        /// execution while the other dummy swings for longer.
        /// </summary>
        [UnityTest]
        public IEnumerator AShieldTap_RaisesItAndHoldsItThroughTheWholeExecution()
        {
            var board = new PoseSnapshot
            {
                Left = Fighter(Side.Left, -HeadOnHalfGapUnits, _rapier),
                Right = Fighter(Side.Right, HeadOnHalfGapUnits, _shield),
            };
            Vec2 centre = PathPointLocal(board, Side.Right, _shield);
            WeaponPath tap = new PathBuilder(_policies)
                .Build(WeaponPath.Empty, new WeaponPath(new[] { ShieldRaisedLocal }), centre, _shield, _rules.Paths).Path;
            WeaponPath swing = Path(board, Side.Left, _rapier, HighSwingMiddleLocal, HighSwingEndLocal);
            SimTime arrival = SimTime.FromSeconds(tap.LengthUnits / _shield.SpeedUnitsPerSecond);

            TurnResult result = Simulate(board, _rapier, _shield, swing, tap);

            Vec2 tapped = ToArena(board, Side.Right, ShieldRaisedLocal);
            float faceOut = TorsoFrame.AngleToArena((ShieldRaisedLocal - _rules.Paths.ShoulderOffsetUnits).AngleDegrees, TurnStartRoot.Of(board.Right),
                Side.Right);
            float worstDrift = 0f;
            float worstTurn = 0f;
            int framesHeld = 0;
            foreach (TimelineFrame frame in result.Timeline.Frames)
            {
                if (frame.Time <= arrival + SimTime.FromMilliseconds(10)) continue;
                BodyPose held = frame.Pose.Right.HeldItem;
                worstDrift = Mathf.Max(worstDrift, Vec2.Distance(held.PositionUnits, tapped));
                worstTurn = Mathf.Max(worstTurn, Mathf.Abs(XMath.DeltaAngleDegrees(held.RotationDegrees, faceOut)));
                framesHeld++;
            }

            double turnSeconds = result.Timeline.Duration.Seconds;
            Report("Shield tap: lead-in {0:0} units ({1:0.000} s); held over {2} frames until {3:0.000} s; worst drift {4:0.000}, worst turn {5:0.00}°.",
                tap.LengthUnits, arrival.Seconds, framesHeld, turnSeconds, worstDrift, worstTurn);

            Assert.That(tap.IsEmpty, Is.False, "D3: a tap is a short lead-in to the tapped spot");
            Assert.That(turnSeconds, Is.GreaterThan(arrival.Seconds + 0.2), "test setup: the turn goes on well after the shield is up");
            Assert.That(framesHeld, Is.GreaterThan(10));
            Assert.That(worstDrift, Is.LessThan(0.5f), "held in place for the whole execution");
            Assert.That(worstTurn, Is.LessThan(0.5f), "face out the whole time");
            Assert.That(EventsOf<ShieldBlockEvent>(result), Is.Empty);
            yield return null;
        }
    }
}
