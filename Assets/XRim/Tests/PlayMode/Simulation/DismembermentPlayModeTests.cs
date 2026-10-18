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
using XRim.Networking;
using XRim.Rules;
using XRim.Rules.Arena;
using XRim.Rules.Events;
using XRim.Rules.Limbs;
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
    /// Dismemberment on real Unity 2D physics (Session 11, GDD §11, §12), turn after turn the way the authority carries the board.
    /// Both dummies are right-handed, mirrored around x = 0. Heights in the torso frame (pelvis = 0): shoulder 100, torso top 120,
    /// head centre 145 (radius 25); arms hang from the shoulder to −140, legs reach the floor at −180. Each scenario logs
    /// "[XRim sever] …".
    /// </summary>
    public sealed class DismembermentPlayModeTests
    {
        /// <summary>Pelvis to pelvis 600: the rapier (arm 240 + 400) reaches the opponent's arm and chest.</summary>
        private const float RapierRangeHalfGapUnits = 300f;

        /// <summary>Pelvis to pelvis 700, the starting gap: nothing touches while both stand idle.</summary>
        private const float StartingHalfGapUnits = 350f;

        /// <summary>Pelvis to pelvis 180: an armless dummy's headbutt (its head 150 forward and 55 down, leaning in) reaches the opponent's chest.</summary>
        private const float HeadbuttHalfGapUnits = 90f;

        /// <summary>
        /// A rapier held 30° up with the hand at full reach: its tip rests at (554, 420), high above the opponent's head, and its
        /// blade never passes x 554, so it can only come down onto the opponent's extended arm (the target's torso front is at 570).
        /// </summary>
        private const float HighGuardDegrees = 30f;

        /// <summary>The high guard's chop ends here, past the target's arm (which crosses x 480 at y 0) and above its blade.</summary>
        private static readonly Vec2 ChopEndLocal = new Vec2(480f, -40f);

        /// <summary>From the low guard straight up in front of the target, then level into its chest (front at 570).</summary>
        private static readonly Vec2 RiseLocal = new Vec2(480f, 60f);
        private static readonly Vec2 ChestLocal = new Vec2(600f, 60f);

        /// <summary>From the low guard into the chest of a dummy 600 away (as in the hit tests).</summary>
        private static readonly Vec2 ChestThrustEndLocal = new Vec2(620f, 100f);

        /// <summary>Arm durability whose 35% cap (6.3) a rapier arm hit (6.4) exceeds: the third capped hit severs.</summary>
        private const float ThreeCappedRapierHitsDurability = 18f;

        /// <summary>
        /// No guard tip cap: Session 14's <see cref="RagdollSettings.GuardMaxTipDistanceUnits"/> (400) would rest a rapier held at full
        /// reach with its tip 400 from the shoulder instead of 640, so the right blade would lie along its own arm and the chop would meet
        /// that blade (a blade contact, no hit) before the arm. The three-hit scenario keeps the guard it was built with (Session 11).
        /// </summary>
        private const float UncappedGuardTipUnits = float.MaxValue;

        private const float Tolerance = 1e-3f;

        private SimulationMode2D _previousMode;
        private ArenaSpace _space;
        private RulesSettings _rules;
        private SimulationSettings _simulation;
        private RagdollSettings _body;
        private WeaponStats _rapier;
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

        /// <summary>A dummy en garde, its guard drawn with <paramref name="body"/> (the shared settings by default); unarmed with a null weapon.</summary>
        private FighterPose Fighter(Side side, float pelvisX, WeaponStats weapon, RagdollSettings body = null) =>
            GuardStance.Create(TorsoAt(pelvisX), side, BodyPart.RightArm, weapon, _simulation.Segmentation, body ?? _body, _rules.Paths, _aim);

        private MatchState State(WeaponId left, WeaponId right) => new MatchState(
            new PerSide<FighterState>(
                new FighterState(Handedness.Right, _rules.Damage.MaxHp, left),
                new FighterState(Handedness.Right, _rules.Damage.MaxHp, right)),
            PerSide<ElectricWallState>.Create(_ => new ElectricWallState()));

        private static TurnPlan Plan(WeaponId weapon, WeaponPath path) => new TurnPlan(weapon, BodyMove.None, path ?? WeaponPath.Empty, default, true);

        private TurnResult Simulate(PoseSnapshot board, MatchState state, WeaponPath left, WeaponPath right, out Unity2DPhysicsWorld world,
            ITurnContactHandler contacts = null)
        {
            world = new Unity2DPhysicsWorld(_space, new RagdollPrefabSet(_sixBodies, _tenBodies));
            _worlds.Add(world);
            var input = new TurnInput(new BoardSnapshot(state, board),
                new PerSide<TurnPlan>(Plan(state.Fighters.Left.CurrentWeapon, left), Plan(state.Fighters.Right.CurrentWeapon, right)), _rules, _simulation);
            var options = new TurnSimulatorOptions { Contacts = contacts ?? new HitContactHandler() };
            return new TurnSimulator(world, _policies, options).Simulate(input);
        }

        /// <summary>Where a side's path starts on a board: its weapon tip (or the guard tip with nothing in hand), as planning reads it.</summary>
        private Vec2 TipLocal(PoseSnapshot board, MatchState state, Side side, WeaponId weapon) =>
            new PoseWeaponTipLocator(_rules, _simulation).ForSide(new BoardSnapshot(state, board), side).TipLocal(weapon);

        private static List<HitLandedEvent> HitsBy(TurnResult result, Side attacker) =>
            result.Timeline.Events.OfType<HitLandedEvent>().Where(hit => hit.Hit.Attacker == attacker).ToList();

        private static string Describe(TurnResult result)
        {
            List<HitLandedEvent> hits = result.Timeline.Events.OfType<HitLandedEvent>().ToList();
            if (hits.Count == 0) return "no hits";
            return string.Join("; ", hits.Select(hit => string.Format(CultureInfo.InvariantCulture, "{0} → {1} with {2} at {3:0.0} ms for {4:0.0}{5}",
                hit.Hit.Attacker, hit.Hit.Part, hit.Hit.Weapon, hit.Time.Milliseconds, hit.Damage.Damage, hit.Hit.IsOffHand ? " (off hand)" : "")));
        }

        private static void Report(string format, params object[] args) =>
            Debug.Log("[XRim sever] " + string.Format(CultureInfo.InvariantCulture, format, args));

        private static bool HasFloorLimb(PoseSnapshot pose, Side owner, BodyPart part) =>
            pose.SeveredLimbs.Any(limb => limb.Owner == owner && limb.Part == part);

        // --- Scenarios -------------------------------------------------------------------------

        /// <summary>
        /// GDD §11, §12 (Decided): three capped hits on the dominant arm sever it, the weapon drops, and from the next turn the dummy
        /// fights on with its other hand at 80% damage. The right dummy holds its rapier with the hand at full reach (its blade starts
        /// at the hand, so the arm is bare); the left chops down onto that arm from a high guard. Arm durability 18: each rapier arm
        /// hit is capped at 6.3, so the third severs. Turns 1–3 start from the same guard (only the rules state carries over); turn 4
        /// starts from turn 3's board.
        /// </summary>
        [UnityTest]
        public IEnumerator ThreeArmHits_Sever_TheWeaponDrops_AndTheNextTurnUsesTheOffHand()
        {
            _rules.Damage.ArmDurability = ThreeCappedRapierHitsDurability;
            _body.GuardHandReachFraction = 1f;
            _body.GuardMaxTipDistanceUnits = UncappedGuardTipUnits;
            _simulation.HitReaction.KnockbackPersists = false;
            var highGuard = new RagdollSettings
            {
                GuardAngleDegrees = HighGuardDegrees, GuardHandReachFraction = 1f, GuardMaxTipDistanceUnits = UncappedGuardTipUnits,
            };
            MatchState state = State(WeaponIds.Rapier, WeaponIds.Rapier);
            TurnResult turn = null;

            for (int number = 1; number <= 3; number++)
            {
                var board = new PoseSnapshot
                {
                    Left = Fighter(Side.Left, -RapierRangeHalfGapUnits, _rapier, highGuard),
                    Right = Fighter(Side.Right, RapierRangeHalfGapUnits, _rapier),
                };
                var chop = new WeaponPath(new[] { TipLocal(board, state, Side.Left, WeaponIds.Rapier), ChopEndLocal });
                turn = Simulate(board, state, chop, null, out _);
                state = turn.FinalBoard.State;
                Report("Turn {0}: {1}; right arm damage {2:0.0} of {3:0}.", number, Describe(turn),
                    state.Fighters.Right.GetLimbDamage(BodyPart.RightArm), ThreeCappedRapierHitsDurability);

                List<HitLandedEvent> hits = HitsBy(turn, Side.Left);
                Assert.That(hits, Is.Not.Empty, $"turn {number}: the chop lands: {Describe(turn)}");
                Assert.That(hits[0].Hit.Part, Is.EqualTo(BodyPart.RightArm), $"turn {number}, test setup: the chop meets the arm first: {Describe(turn)}");
                Assert.That(hits[0].Damage.LimbDamage, Is.EqualTo(ThreeCappedRapierHitsDurability * 0.35f).Within(Tolerance), "§11: capped at 35%");
                bool severedNow = turn.Timeline.Events.OfType<LimbSeveredEvent>().Any();
                Assert.That(severedNow, Is.EqualTo(number == 3), $"turn {number}: §11 severs only at the third capped hit");
            }

            PoseSnapshot afterSever = turn.FinalBoard.Pose;
            Assert.That(state.Fighters.Right.IsSevered(BodyPart.RightArm), Is.True);
            Assert.That(turn.Timeline.Events.OfType<WeaponDroppedEvent>().Single().Weapon, Is.EqualTo(WeaponIds.Rapier), "§12: the weapon drops");
            Assert.That(HasFloorLimb(afterSever, Side.Right, BodyPart.RightArm), Is.True, "the arm lies on the floor of the next board");
            SeveredLimbPose arm = afterSever.SeveredLimbs.Single(limb => limb.Owner == Side.Right && limb.Part == BodyPart.RightArm);
            Assert.That(arm.HasLower, Is.True, "ten bodies: it keeps its forearm (S1)");
            Assert.That(arm.Pose.PositionUnits.Y, Is.LessThan(TorsoAt(0f).PositionUnits.Y + _rules.Paths.ShoulderOffsetUnits.Y - 20f), "the arm fell");
            Assert.That(afterSever.Right.HasHeldItem, Is.False);
            Assert.That(afterSever.Right.HasDroppedItem, Is.True, "the dropped rapier lies where it fell");

            // Turn 4: the left stands unarmed; the right thrusts with its other hand (G2: the rapier is back, at the guard).
            var nextBoard = new PoseSnapshot
            {
                Left = Fighter(Side.Left, -RapierRangeHalfGapUnits, null),
                Right = afterSever.Right.Clone(),
                SeveredLimbs = new List<SeveredLimbPose>(afterSever.SeveredLimbs),
            };
            MatchState nextState = state.Clone();
            var thrust = new WeaponPath(new[] { TipLocal(nextBoard, nextState, Side.Right, WeaponIds.Rapier), RiseLocal, ChestLocal });
            TurnResult offHand = Simulate(nextBoard, nextState, null, thrust, out Unity2DPhysicsWorld offHandWorld);
            Report("Turn 4 with the other hand: {0}", Describe(offHand));

            Assert.That(offHandWorld.GetRagdoll(Side.Right).HeldArm, Is.EqualTo(BodyPart.LeftArm), "§12: the other hand holds it");
            List<HitLandedEvent> offHits = HitsBy(offHand, Side.Right);
            Assert.That(offHits, Is.Not.Empty, "the off-hand thrust lands: " + Describe(offHand));
            Assert.That(offHits[0].Hit.IsOffHand, Is.True);
            Assert.That(offHits[0].Hit.Part, Is.EqualTo(BodyPart.Torso), "test setup: the thrust meets the chest: " + Describe(offHand));
            Assert.That(offHits[0].Damage.Damage, Is.EqualTo(_rapier.BaseDamage * _rules.Damage.OffHandDamageMultiplier).Within(Tolerance),
                "§12: 80% damage");
            Assert.That(HasFloorLimb(offHand.FinalBoard.Pose, Side.Right, BodyPart.RightArm), Is.True, "the arm is still on the floor");
            yield return null;
        }

        /// <summary>
        /// GDD §12 (Decided): a severed limb stays on the floor turn after turn, and a remaining arm can wield it as a club; picking
        /// the club retrieves it automatically (the §12 default). The force-sever cheat cuts the right dummy's leg in turn 1; turn 2
        /// is idle; in turn 3 the left dummy (G1: anyone's limb) picks the club.
        /// </summary>
        [UnityTest]
        public IEnumerator ASeveredLimb_StaysOnTheFloor_AndIsPickedUpAsAClub()
        {
            var board = new PoseSnapshot
            {
                Left = Fighter(Side.Left, -StartingHalfGapUnits, null),
                Right = Fighter(Side.Right, StartingHalfGapUnits, null),
            };
            MatchState state = State(WeaponIds.Rapier, WeaponIds.Rapier);
            var cheat = new ScriptedSeverContactHandler(new HitContactHandler());
            cheat.QueueSever(Side.Right, BodyPart.LeftLeg);

            TurnResult cut = Simulate(board, state, null, null, out _, cheat);
            TurnResult idle = Simulate(cut.FinalBoard.Pose, cut.FinalBoard.State, null, null, out _);
            MatchState pickState = idle.FinalBoard.State.Clone();
            pickState.Fighters.Left.CurrentWeapon = WeaponIds.SeveredLimb;
            TurnResult pick = Simulate(idle.FinalBoard.Pose, pickState, null, null, out Unity2DPhysicsWorld pickWorld);

            SeveredLimbPose leg = idle.FinalBoard.Pose.SeveredLimbs.Single(limb => limb.Owner == Side.Right && limb.Part == BodyPart.LeftLeg);
            Report("The leg after the idle turn: hip end at ({0:0}, {1:0}); the club holder: {2}.", leg.Pose.PositionUnits.X,
                leg.Pose.PositionUnits.Y, pick.FinalBoard.State.Fighters.Left.HeldLimb);

            Assert.That(cut.Timeline.Events.OfType<LimbSeveredEvent>().Single().Time, Is.EqualTo(SimTime.Zero), "the cheat severs at once");
            Assert.That(HasFloorLimb(cut.FinalBoard.Pose, Side.Right, BodyPart.LeftLeg), Is.True, "it lies on the floor after the cut");
            Assert.That(leg.HasLower, Is.True, "ten bodies: it keeps its shin (S1)");
            Assert.That(leg.Pose.PositionUnits.Y, Is.LessThan(_body.LegLengthUnits * 0.5f), "it rests on the floor across the next turn");
            Assert.That(pick.FinalBoard.State.Fighters.Left.HeldLimb, Is.EqualTo(new SeveredLimbRef(Side.Right, BodyPart.LeftLeg)));
            Assert.That(pickWorld.GetRagdoll(Side.Left).ActiveHeldItem.name, Does.Contain(WeaponIds.SeveredLimb.Value), "the club is in hand");
            Assert.That(pick.FinalBoard.Pose.Left.HasHeldItem, Is.True);
            Assert.That(HasFloorLimb(pick.FinalBoard.Pose, Side.Right, BodyPart.LeftLeg), Is.False, "picked up: no longer on the floor");
            yield return null;
        }

        /// <summary>GDD §11 (Decided): at 0 HP the torso splits in two as the death animation, by logic; the dead dummy goes limp (S2).</summary>
        [UnityTest]
        public IEnumerator AKillingHit_SplitsTheTorsoInTwo_AndTheDummyGoesLimp()
        {
            var board = new PoseSnapshot
            {
                Left = Fighter(Side.Left, -RapierRangeHalfGapUnits, _rapier),
                Right = Fighter(Side.Right, RapierRangeHalfGapUnits, null),
            };
            MatchState state = State(WeaponIds.Rapier, WeaponIds.Rapier);
            state.Fighters.Right.Hp = 1f;
            var thrust = new WeaponPath(new[] { TipLocal(board, state, Side.Left, WeaponIds.Rapier), ChestThrustEndLocal });

            TurnResult result = Simulate(board, state, thrust, null, out Unity2DPhysicsWorld world);

            FighterPose dead = result.FinalBoard.Pose.Right;
            float splitHeight = TorsoAt(0f).PositionUnits.Y + _body.TorsoHeightUnits * Ragdoll.TorsoHalfFraction;
            Report("Kill: {0}; top half ended at ({1:0}, {2:0}), split at height {3:0}.", Describe(result), dead.TorsoTop.PositionUnits.X,
                dead.TorsoTop.PositionUnits.Y, splitHeight);

            Assert.That(result.Timeline.Events.OfType<FighterDiedEvent>().Single().Side, Is.EqualTo(Side.Right));
            Ragdoll ragdoll = world.GetRagdoll(Side.Right);
            Assert.That(ragdoll.IsTorsoSplit, Is.True, "§11: the torso splits in two");
            Assert.That(ragdoll.GetBody(BodyPart.Torso).gameObject.activeSelf, Is.False, "the halves took its place");
            Assert.That(ragdoll.TorsoTop.gameObject.activeSelf && ragdoll.TorsoBottom.gameObject.activeSelf, Is.True);
            Assert.That(dead.IsTorsoSplit, Is.True, "the next board shows the split");
            Assert.That(dead.TorsoTop.PositionUnits.Y, Is.LessThan(splitHeight - 20f), "limp: the top half falls");
            yield return null;
        }

        /// <summary>
        /// GDD §12: with both arms gone the dummy fights with body blows; by default the drawn path steers the head. The left dummy has
        /// no arms; its head goes 150 forward and 55 down along its path, the body leaning in behind it (about 52°), into the chest of a
        /// dummy 180 away (its front 60 past the middle of the gap): leaning in, the head arrives before the chest.
        /// </summary>
        [UnityTest]
        public IEnumerator AnArmlessDummy_HeadbuttsAlongItsPath()
        {
            var board = new PoseSnapshot
            {
                Left = Fighter(Side.Left, -HeadbuttHalfGapUnits, null),
                Right = Fighter(Side.Right, HeadbuttHalfGapUnits, null),
            };
            MatchState state = State(WeaponIds.BodyBlow, WeaponIds.Rapier);
            state.Fighters.Left.MarkSevered(BodyPart.LeftArm);
            state.Fighters.Left.MarkSevered(BodyPart.RightArm);
            Vec2 headCentre = TipLocal(board, state, Side.Left, WeaponIds.BodyBlow);
            var headbutt = new WeaponPath(new[] { headCentre, headCentre + new Vec2(150f, -55f) });

            TurnResult result = Simulate(board, state, headbutt, null, out Unity2DPhysicsWorld world);

            Report("Headbutt from ({0:0}, {1:0}): {2}", headCentre.X, headCentre.Y, Describe(result));
            Assert.That(world.GetRagdoll(Side.Left).GetBody(BodyPart.LeftArm).gameObject.activeSelf, Is.False, "test setup: no arms");
            Assert.That(headCentre.Y, Is.EqualTo(_body.TorsoHeightUnits + _rules.FindWeapon(WeaponIds.BodyBlow).InkThicknessUnits * 0.5f).Within(1f),
                "the path starts at the head's centre");
            List<HitLandedEvent> hits = HitsBy(result, Side.Left);
            Assert.That(hits, Is.Not.Empty, "the headbutt lands: " + Describe(result));
            Assert.That(hits[0].Hit.Weapon, Is.EqualTo(WeaponIds.BodyBlow));
            Assert.That(hits[0].Hit.IsOffHand, Is.False, "no hand at all");
            Assert.That(result.Report.ResolvedState.Fighters.Right.Hp, Is.LessThan(_rules.Damage.MaxHp));
            yield return null;
        }
    }
}
