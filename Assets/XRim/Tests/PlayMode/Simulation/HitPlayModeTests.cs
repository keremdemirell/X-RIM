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
using Object = UnityEngine.Object;

namespace XRim.Tests.PlayMode.Simulation
{
    /// <summary>
    /// The hit rules on real Unity 2D physics (Session 06). Both dummies are right-handed, standing en garde and mirrored
    /// around x = 0. Heights in the torso frame (pelvis = 0): shoulder 100, torso top 120, head centre 145 (radius 25); arms
    /// hang from the shoulder to −140, legs reach the floor at −180. Each scenario places its target so the first thing the
    /// weapon touches is known, and logs "[XRim hits] …" with what was hit.
    /// </summary>
    public sealed class HitPlayModeTests
    {
        /// <summary>A rapier held 15° up: its tip rests above the opponent's head, clear of every body.</summary>
        private const float RaisedGuardDegrees = 15f;

        /// <summary>Pelvis to pelvis 450: the mace (arm 240 + 220) can reach the opponent's head.</summary>
        private const float MaceRangeHalfGapUnits = 225f;

        /// <summary>Pelvis to pelvis 600: an unarmed target well inside the rapier's reach (arm 240 + 400).</summary>
        private const float RapierRangeHalfGapUnits = 300f;

        /// <summary>Pelvis to pelvis 580: the front of the target's legs is in reach of a low rapier thrust.</summary>
        private const float LegRangeHalfGapUnits = 290f;

        /// <summary>Pelvis to pelvis 400: the mace reaches the target's chest.</summary>
        private const float CloseHalfGapUnits = 200f;

        /// <summary>From the raised guard, down onto the head of a dummy 450 away: about 60 units, under 0.07 s.</summary>
        private static readonly Vec2 HeadPokeEndLocal = new Vec2(440f, 150f);

        /// <summary>From the low guard, straight up into the head of a dummy 450 away: about 320 units, over a second at mace speed.</summary>
        private static readonly Vec2 MaceSwingEndLocal = new Vec2(430f, 150f);

        /// <summary>From the low guard up into the chest of a dummy 600 away.</summary>
        private static readonly Vec2 ChestThrustEndLocal = new Vec2(620f, 100f);

        /// <summary>Level along the floor into the shins of a dummy 580 away, under its hanging hands.</summary>
        private static readonly Vec2 ShinThrustEndLocal = new Vec2(575f, -160f);

        /// <summary>
        /// From the low guard to 15 past the front of the chest of a dummy 400 away (its front is at 370). The mace's box ends
        /// flat at its tip, so the tip itself must cross the front; ending only a little past it, the kinematic mace does not
        /// hold the chest pushed in far once the knockback is off.
        /// </summary>
        private static readonly Vec2 MaceChestEndLocal = new Vec2(385f, 60f);

        private const float Tolerance = 1e-3f;

        private SimulationMode2D _previousMode;
        private ArenaSpace _space;
        private RulesSettings _rules;
        private SimulationSettings _simulation;
        private RagdollSettings _body;
        private WeaponStats _rapier;
        private WeaponStats _mace;
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

        /// <summary>A dummy en garde; <paramref name="weapon"/> null stands it unarmed with both arms hanging.</summary>
        private FighterPose Fighter(Side side, float pelvisX, WeaponStats weapon, float? guardAngleDegrees = null)
        {
            RagdollSettings body = _body;
            if (guardAngleDegrees.HasValue) body = new RagdollSettings { GuardAngleDegrees = guardAngleDegrees.Value };
            return GuardStance.Create(TorsoAt(pelvisX), side, BodyPart.RightArm, weapon, _simulation.Segmentation, body, _rules.Paths, _aim);
        }

        private MatchState State(WeaponId left, WeaponId right) => new MatchState(
            new PerSide<FighterState>(
                new FighterState(Handedness.Right, _rules.Damage.MaxHp, left),
                new FighterState(Handedness.Right, _rules.Damage.MaxHp, right)),
            PerSide<ElectricWallState>.Create(_ => new ElectricWallState()));

        private static TurnPlan Plan(WeaponStats weapon, WeaponPath path) =>
            new TurnPlan(weapon != null ? weapon.WeaponId : WeaponIds.Rapier, BodyMove.None, path ?? WeaponPath.Empty, default, true);

        private TurnResult Simulate(PoseSnapshot board, MatchState state, TurnPlan left, TurnPlan right)
        {
            var world = new Unity2DPhysicsWorld(_space, new RagdollPrefabSet(_sixBodies, _tenBodies));
            _worlds.Add(world);
            var input = new TurnInput(new BoardSnapshot(state, board), new PerSide<TurnPlan>(left, right), _rules, _simulation);
            return new TurnSimulator(world, _policies).Simulate(input);
        }

        private static Vec2 Tip(BodyPose grip, WeaponStats weapon) =>
            grip.PositionUnits + Vec2.FromAngleDegrees(grip.RotationDegrees) * weapon.LengthUnits;

        /// <summary>The weapon tip on the frozen board, in the torso frame the turn will use: where a path starts.</summary>
        private static Vec2 TipLocal(PoseSnapshot board, Side side, WeaponStats weapon) =>
            TorsoFrame.ToLocal(Tip(board.Get(side).HeldItem, weapon), TurnStartRoot.Of(board.Get(side)), side);

        private static WeaponPath Path(PoseSnapshot board, Side side, WeaponStats weapon, Vec2 endLocal) =>
            new WeaponPath(new[] { TipLocal(board, side, weapon), endLocal });

        private static List<HitLandedEvent> Hits(TurnResult result) => result.Timeline.Events.OfType<HitLandedEvent>().ToList();

        private static List<HitLandedEvent> HitsBy(TurnResult result, Side attacker) =>
            Hits(result).Where(hit => hit.Hit.Attacker == attacker).ToList();

        private static string Describe(TurnResult result)
        {
            List<HitLandedEvent> hits = Hits(result);
            if (hits.Count == 0) return "no hits";
            return string.Join("; ", hits.Select(hit => string.Format(CultureInfo.InvariantCulture, "{0} → {1} at {2:0.0} ms for {3:0.0}",
                hit.Hit.Attacker, hit.Hit.Part, hit.Time.Milliseconds, hit.Damage.HpDamage)));
        }

        private static TimelineFrame FrameAt(TurnResult result, SimTime time) =>
            result.Timeline.Frames.FirstOrDefault(frame => frame.Time >= time) ?? result.Timeline.Frames.Last();

        private float Hp(TurnResult result, Side side) => result.Report.ResolvedState.Fighters[side].Hp;

        private static float PelvisX(PoseSnapshot pose, Side side) => pose.Get(side).Get(BodyPart.Torso).PositionUnits.X;

        private static void Report(string format, params object[] args) =>
            Debug.Log("[XRim hits] " + string.Format(CultureInfo.InvariantCulture, format, args));

        // --- Scenarios -------------------------------------------------------------------------

        /// <summary>
        /// GDD §9 (Decided): the first weapon to reach a valid hitbox lands, and a dummy hit before its own attack lands has that
        /// attack interrupted. A short rapier poke to the head (D15: the head interrupts) beats a slow mace swing that, unopposed,
        /// lands. Control: the same swing against a dummy that keeps its rapier still.
        /// </summary>
        [UnityTest]
        public IEnumerator AShortRapierThrust_InterruptsASlowMaceSwing()
        {
            var board = new PoseSnapshot
            {
                Left = Fighter(Side.Left, -MaceRangeHalfGapUnits, _rapier, RaisedGuardDegrees),
                Right = Fighter(Side.Right, MaceRangeHalfGapUnits, _mace),
            };
            WeaponPath swing = Path(board, Side.Right, _mace, MaceSwingEndLocal);
            WeaponPath poke = Path(board, Side.Left, _rapier, HeadPokeEndLocal);
            Vec2 maceStart = Tip(board.Right.HeldItem, _mace);

            TurnResult unopposed = Simulate(board, State(WeaponIds.Rapier, WeaponIds.Mace), Plan(_rapier, null), Plan(_mace, swing));
            TurnResult contested = Simulate(board, State(WeaponIds.Rapier, WeaponIds.Mace), Plan(_rapier, poke), Plan(_mace, swing));

            float unopposedTravel = Vec2.Distance(Tip(unopposed.FinalBoard.Pose.Right.HeldItem, _mace), maceStart);
            float contestedTravel = Vec2.Distance(Tip(contested.FinalBoard.Pose.Right.HeldItem, _mace), maceStart);
            Report("Mace swing unopposed: {0} (mace tip travelled {1:0}). Against the poke: {2} (mace tip travelled {3:0}).",
                Describe(unopposed), unopposedTravel, Describe(contested), contestedTravel);

            Assert.That(HitsBy(unopposed, Side.Right), Is.Not.Empty, "control: unopposed, the slow mace swing lands");
            Assert.That(unopposedTravel, Is.GreaterThan(200f), "control: the mace travels its swing");

            List<HitLandedEvent> hits = Hits(contested);
            Assert.That(hits, Is.Not.Empty, "the poke lands: " + Describe(contested));
            Assert.That(hits[0].Hit.Attacker, Is.EqualTo(Side.Left), "the rapier is first: " + Describe(contested));
            Assert.That(hits[0].Hit.Part, Is.EqualTo(BodyPart.Head), "test setup: the poke meets the head first: " + Describe(contested));
            Assert.That(hits[0].Time.Seconds, Is.LessThan(0.15), "a short thrust arrives early");

            AttackInterruptedEvent interrupt = contested.Timeline.Events.OfType<AttackInterruptedEvent>().Single();
            Assert.That(interrupt.Interrupted, Is.EqualTo(Side.Right));
            Assert.That(interrupt.Time, Is.EqualTo(hits[0].Time), "cancelled at the instant of the hit");
            Assert.That(HitsBy(contested, Side.Right), Is.Empty, "the interrupted mace lands nothing");
            Assert.That(Hp(contested, Side.Left), Is.EqualTo(_rules.Damage.MaxHp), "the rapier's dummy is untouched");
            Assert.That(Hp(contested, Side.Right), Is.LessThanOrEqualTo(_rules.Damage.MaxHp - _rapier.BaseDamage * 2.5f + Tolerance));
            Assert.That(contestedTravel, Is.LessThan(80f), "the mace stopped near where it was cut off");
            yield return null;
        }

        [UnityTest]
        public IEnumerator AChestThrust_HitsTheTorso_ForBaseDamage()
        {
            var board = new PoseSnapshot
            {
                Left = Fighter(Side.Left, -RapierRangeHalfGapUnits, _rapier),
                Right = Fighter(Side.Right, RapierRangeHalfGapUnits, null),
            };

            TurnResult result = Simulate(board, State(WeaponIds.Rapier, WeaponIds.Rapier),
                Plan(_rapier, Path(board, Side.Left, _rapier, ChestThrustEndLocal)), Plan(null, null));

            Report("Chest thrust: {0}", Describe(result));
            List<HitLandedEvent> hits = Hits(result);
            Assert.That(hits, Is.Not.Empty, "the thrust reaches the chest");
            Assert.That(hits[0].Hit.Part, Is.EqualTo(BodyPart.Torso), "first contact: " + Describe(result));
            Assert.That(hits[0].Damage.HpDamage, Is.EqualTo(_rapier.BaseDamage).Within(Tolerance), "torso × 1.0");
            Assert.That(hits[0].Damage.LimbDamage, Is.EqualTo(0f));
            yield return null;
        }

        [UnityTest]
        public IEnumerator ALowThrust_HitsALeg_AndAddsLimbDamage()
        {
            var board = new PoseSnapshot
            {
                Left = Fighter(Side.Left, -LegRangeHalfGapUnits, _rapier),
                Right = Fighter(Side.Right, LegRangeHalfGapUnits, null),
            };

            TurnResult result = Simulate(board, State(WeaponIds.Rapier, WeaponIds.Rapier),
                Plan(_rapier, Path(board, Side.Left, _rapier, ShinThrustEndLocal)), Plan(null, null));

            Report("Shin thrust: {0}", Describe(result));
            List<HitLandedEvent> hits = Hits(result);
            Assert.That(hits, Is.Not.Empty, "the thrust reaches the shins");
            BodyPart part = hits[0].Hit.Part;
            Assert.That(part == BodyPart.LeftLeg || part == BodyPart.RightLeg, Is.True, "first contact: " + Describe(result));
            float damage = _rapier.BaseDamage * _rules.HitZones.LegMultiplier;
            Assert.That(hits[0].Damage.HpDamage, Is.EqualTo(damage).Within(Tolerance));
            Assert.That(hits[0].Damage.LimbDamage, Is.EqualTo(damage).Within(Tolerance), "below the 35% cap, the whole hit");
            Assert.That(result.Report.ResolvedState.Fighters[Side.Right].GetLimbDamage(part), Is.GreaterThanOrEqualTo(damage - Tolerance));
            yield return null;
        }

        /// <summary>D26 with one hit per turn, and D1's hand-off: the blade stops at the hit and is pulled back, still in the hand.</summary>
        [UnityTest]
        public IEnumerator ALastHit_RecoilsBackAlongThePath_AndTheBladeStaysInTheHand()
        {
            _rules.Damage.MaxHitsPerWeaponPerTurn = 1;
            var board = new PoseSnapshot
            {
                Left = Fighter(Side.Left, -RapierRangeHalfGapUnits, _rapier),
                Right = Fighter(Side.Right, RapierRangeHalfGapUnits, null),
            };
            WeaponPath thrust = Path(board, Side.Left, _rapier, ChestThrustEndLocal);

            TurnResult result = Simulate(board, State(WeaponIds.Rapier, WeaponIds.Rapier), Plan(_rapier, thrust), Plan(null, null));

            HitLandedEvent hit = Hits(result).First();
            Vec2 tipAtHit = Tip(FrameAt(result, hit.Time).Pose.Left.HeldItem, _rapier);
            FighterPose final = result.FinalBoard.Pose.Left;
            Vec2 finalTip = Tip(final.HeldItem, _rapier);
            Vec2 thrustDirection = TorsoFrame.ToArena(thrust.Points[1], TurnStartRoot.Of(board.Left), Side.Left) -
                                   TorsoFrame.ToArena(thrust.Points[0], TurnStartRoot.Of(board.Left), Side.Left);
            float along = Vec2.Dot(finalTip - tipAtHit, thrustDirection.Normalized);
            Vec2 shoulder = TorsoFrame.ToArena(_rules.Paths.ShoulderOffsetUnits, TurnStartRoot.Of(final), Side.Left);
            float gripFromShoulder = Vec2.Distance(final.HeldItem.PositionUnits, shoulder);
            Report("Recoil: {0}; the tip ends {1:0.0} along the thrust from where it hit; the grip ends {2:0} from the shoulder",
                Describe(result), along, gripFromShoulder);

            Assert.That(Hits(result), Has.Count.EqualTo(1), "one hit per turn here");
            Assert.That(along, Is.LessThan(-10f), "pulled back along its path (recoil " + _simulation.HitReaction.RecoilDistanceUnits + ")");
            Assert.That(gripFromShoulder, Is.LessThan(_rules.Paths.ArmLengthUnits + 50f), "still in the hand");
            Assert.That(finalTip.Y, Is.GreaterThan(_body.LegLengthUnits * 0.5f), "held up, not dropped to the floor");
            yield return null;
        }

        /// <summary>E1: a landed hit pushes the victim back and it stays there, so the next turn starts from there.</summary>
        [UnityTest]
        public IEnumerator AMaceHit_KnocksTheVictimBack_AndTheNextTurnStartsThere()
        {
            var board = new PoseSnapshot
            {
                Left = Fighter(Side.Left, -CloseHalfGapUnits, _mace),
                Right = Fighter(Side.Right, CloseHalfGapUnits, null),
            };
            WeaponPath swing = Path(board, Side.Left, _mace, MaceChestEndLocal);
            float knock = _mace.Mass * _simulation.HitReaction.KnockbackUnitsPerWeaponMass;

            TurnResult knocked = Simulate(board, State(WeaponIds.Mace, WeaponIds.Rapier), Plan(_mace, swing), Plan(null, null));
            TurnResult nextTurn = Simulate(knocked.FinalBoard.Pose, knocked.FinalBoard.State, Plan(_mace, null), Plan(null, null));
            _simulation.HitReaction.KnockbackPersists = false;
            TurnResult springsBack = Simulate(board, State(WeaponIds.Mace, WeaponIds.Rapier), Plan(_mace, swing), Plan(null, null));

            float moved = PelvisX(knocked.FinalBoard.Pose, Side.Right) - CloseHalfGapUnits;
            float drift = PelvisX(nextTurn.FinalBoard.Pose, Side.Right) - PelvisX(knocked.FinalBoard.Pose, Side.Right);
            float sprungBack = PelvisX(springsBack.FinalBoard.Pose, Side.Right) - CloseHalfGapUnits;
            Report("Knockback {0:0} expected: pelvis moved {1:0.0}; next idle turn drifted {2:0.0}; with the flag off it ended {3:0.0} away. {4}",
                knock, moved, drift, sprungBack, Describe(knocked));

            Assert.That(Hits(knocked), Is.Not.Empty, "the mace reaches the chest");
            Assert.That(Hits(knocked)[0].Hit.Part, Is.EqualTo(BodyPart.Torso), "first contact: " + Describe(knocked));
            Assert.That(moved, Is.GreaterThan(knock * 0.66f), "knocked back away from the attacker, and it stayed");
            Assert.That(Mathf.Abs(drift), Is.LessThan(10f), "the next turn starts where the knock left it");
            Assert.That(sprungBack, Is.LessThan(knock * 0.5f), "with the flag off the standing spring pulls it back");
            yield return null;
        }
    }
}
