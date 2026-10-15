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
    /// Every weapon of the roster on real Unity 2D physics (Session 14, GDD §6, §7): each of the five weapons, held by the left
    /// dummy en garde, is thrown at the chest of an unarmed dummy placed so the weapon's reach just gets there, and must land its
    /// own base damage. Two spears at the starting gap must start apart (the guard tip cap). Each test logs "[XRim roster] …".
    /// </summary>
    public sealed class RosterPlayModeTests
    {
        /// <summary>How far short of the weapon's full reach (arm plus weapon) the target's pelvis stands, so the chest is inside it.</summary>
        private const float ReachMarginUnits = 60f;

        /// <summary>From the pelvis to the front of the torso, and how far past the front a path ends so the tip crosses it.</summary>
        private const float TorsoHalfWidthUnits = 30f;

        private const float PastTheFrontUnits = 15f;
        private const float ChestHeightLocalUnits = 60f;
        private const float Tolerance = 1e-3f;

        private SimulationMode2D _previousMode;
        private ArenaSpace _space;
        private RulesSettings _rules;
        private SimulationSettings _simulation;
        private RagdollSettings _body;
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

        private FighterPose Fighter(Side side, float pelvisX, WeaponStats weapon) =>
            GuardStance.Create(new BodyPose(new Vec2(pelvisX, _body.LegLengthUnits), 0f), side, BodyPart.RightArm, weapon,
                _simulation.Segmentation, _body, _rules.Paths, _aim);

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

        /// <summary>The held item's path point (a weapon's tip, the shield's centre) on a board, in the arena.</summary>
        private static Vec2 PathPoint(FighterPose pose, WeaponStats weapon) => HeldItemShape.Of(weapon).PathPointAt(pose.HeldItem);

        private static List<HitLandedEvent> HitsBy(TurnResult result, Side attacker) =>
            result.Timeline.Events.OfType<HitLandedEvent>().Where(hit => hit.Hit.Attacker == attacker).ToList();

        private static void Report(string format, params object[] args) =>
            Debug.Log("[XRim roster] " + string.Format(CultureInfo.InvariantCulture, format, args));

        // --- One thrown weapon, whichever it is -----------------------------------------------------

        /// <summary>
        /// The left dummy throws <paramref name="weaponId"/> in a straight line from where it rests to just past the front of an
        /// unarmed dummy's chest, standing so the weapon's full reach is 60 units more than it needs.
        /// </summary>
        private void ARoster_Weapon_LandsItsBaseDamageOnTheChest(WeaponId weaponId)
        {
            WeaponStats weapon = _rules.FindWeapon(weaponId);
            float reach = _rules.Paths.ArmLengthUnits + weapon.ReachBeyondHandUnits;
            float gap = reach - ReachMarginUnits;
            var board = new PoseSnapshot
            {
                Left = Fighter(Side.Left, -gap * 0.5f, weapon),
                Right = Fighter(Side.Right, gap * 0.5f, null),
            };
            Vec2 start = TorsoFrame.ToLocal(PathPoint(board.Left, weapon), TurnStartRoot.Of(board.Left), Side.Left);
            var end = new Vec2(gap - TorsoHalfWidthUnits + PastTheFrontUnits, ChestHeightLocalUnits);
            Assert.That(Vec2.Distance(end, _rules.Paths.ShoulderOffsetUnits), Is.LessThan(reach), "test setup: the end is inside the reach");

            TurnResult result = Simulate(board, State(weaponId, WeaponIds.Rapier), Plan(weapon, new WeaponPath(new[] { start, end })),
                Plan(null, null));

            List<HitLandedEvent> hits = HitsBy(result, Side.Left);
            Report("{0}: gap {1:0}, path {2:0} long, {3} hit(s): {4}", weaponId.Value, gap, Vec2.Distance(start, end), hits.Count,
                string.Join("; ", hits.Select(hit => string.Format(CultureInfo.InvariantCulture, "{0} for {1:0.0}", hit.Hit.Part,
                    hit.Damage.Damage))));
            Assert.That(result.Timeline.Frames.Count, Is.GreaterThan(1), "the turn ran");
            Assert.That(hits, Is.Not.Empty, weaponId.Value + " reaches and hits the chest");
            HitLandedEvent first = hits[0];
            Assert.That(first.Hit.Weapon, Is.EqualTo(weaponId));
            float expected = weapon.BaseDamage * _rules.HitZones.MultiplierFor(first.Hit.Part.ToHitZone());
            Assert.That(first.Damage.Damage, Is.EqualTo(expected).Within(Tolerance), "base damage × the zone it met first");
            Assert.That(result.Report.ResolvedState.Fighters[Side.Right].Hp, Is.LessThan(_rules.Damage.MaxHp));
            Assert.That(result.Report.ResolvedState.Fighters[Side.Left].Hp, Is.EqualTo(_rules.Damage.MaxHp), "the unarmed dummy hit nothing");
        }

        [UnityTest]
        public IEnumerator TheRapier_LandsItsBaseDamage()
        {
            ARoster_Weapon_LandsItsBaseDamageOnTheChest(WeaponIds.Rapier);
            yield return null;
        }

        [UnityTest]
        public IEnumerator TheSword_LandsItsBaseDamage()
        {
            ARoster_Weapon_LandsItsBaseDamageOnTheChest(WeaponIds.Sword);
            yield return null;
        }

        [UnityTest]
        public IEnumerator TheMace_LandsItsBaseDamage()
        {
            ARoster_Weapon_LandsItsBaseDamageOnTheChest(WeaponIds.Mace);
            yield return null;
        }

        [UnityTest]
        public IEnumerator TheSpear_LandsItsBaseDamage_FromItsCappedGuard()
        {
            ARoster_Weapon_LandsItsBaseDamageOnTheChest(WeaponIds.Spear);
            yield return null;
        }

        [UnityTest]
        public IEnumerator TheShieldBash_LandsItsLowDamage()
        {
            ARoster_Weapon_LandsItsBaseDamageOnTheChest(WeaponIds.Shield);
            yield return null;
        }

        // --- The long weapon's stance ---------------------------------------------------------------

        /// <summary>
        /// Two spears en garde at the starting gap: without the guard tip cap they crossed each other and sat below the floor. Held
        /// still for a turn they touch nothing, and both tips end above the floor.
        /// </summary>
        [UnityTest]
        public IEnumerator TwoSpears_AtTheStartingGap_StandApartAndAboveTheFloor()
        {
            WeaponStats spear = _rules.FindWeapon(WeaponIds.Spear);
            float half = _rules.Arena.StartingGapUnits * 0.5f;
            var board = new PoseSnapshot
            {
                Left = Fighter(Side.Left, -half, spear),
                Right = Fighter(Side.Right, half, spear),
            };

            TurnResult result = Simulate(board, State(WeaponIds.Spear, WeaponIds.Spear), Plan(spear, null), Plan(spear, null));

            float leftTipY = PathPoint(result.FinalBoard.Pose.Left, spear).Y;
            float rightTipY = PathPoint(result.FinalBoard.Pose.Right, spear).Y;
            float leftTipX = PathPoint(result.FinalBoard.Pose.Left, spear).X;
            float rightTipX = PathPoint(result.FinalBoard.Pose.Right, spear).X;
            Report("Two spears at the starting gap: tips at x {0:0} and {1:0}, heights {2:0} and {3:0}", leftTipX, rightTipX, leftTipY, rightTipY);

            Assert.That(result.Timeline.Events.OfType<WeaponClashEvent>(), Is.Empty, "the spears do not touch");
            Assert.That(result.Timeline.Events.OfType<HitLandedEvent>(), Is.Empty);
            Assert.That(leftTipX, Is.LessThan(rightTipX), "the tips have not crossed");
            Assert.That(leftTipY, Is.GreaterThan(0f), "above the floor");
            Assert.That(rightTipY, Is.GreaterThan(0f), "above the floor");
            yield return null;
        }
    }
}
