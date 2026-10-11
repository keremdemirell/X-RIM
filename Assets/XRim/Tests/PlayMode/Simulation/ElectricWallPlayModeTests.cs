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
    /// The electric wall on real Unity 2D physics (Session 12, GDD §13; the designer's geometry P1, 2026-10-11), turn after turn the way
    /// the authority carries the board, each turn locked the way the match locks it. Both dummies start en garde on the starting board
    /// (pelvises 700 apart, mirrored around x = 0) with rapiers; only the left one retreats. Each scenario logs "[XRim wall] …".
    /// </summary>
    public sealed class ElectricWallPlayModeTests
    {
        private const float Tolerance = 1e-3f;

        private readonly ElectricWallRules _wallRules = new ElectricWallRules();
        private readonly AimFromShoulderModel _aim = new AimFromShoulderModel();
        private readonly List<Unity2DPhysicsWorld> _worlds = new List<Unity2DPhysicsWorld>();
        private SimulationMode2D _previousMode;
        private ArenaSpace _space;
        private RulesSettings _rules;
        private SimulationSettings _simulation;
        private RulePolicies _policies;
        private Ragdoll _sixBodies;
        private Ragdoll _tenBodies;

        [SetUp]
        public void SetUp()
        {
            _previousMode = Physics2D.simulationMode;
            Physics2D.simulationMode = SimulationMode2D.Script;
            _space = new ArenaSpace(ArenaSpaceConfig.DefaultWorldUnitsPerArenaUnit);
            _rules = GddStartingValues.CreateRulesSettings();
            _simulation = new SimulationSettings();
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
            Ragdoll ragdoll = PlaceholderRagdollBuilder.Build(new RagdollBuildSpec(_simulation.Ragdoll, _rules.Paths, segmentation, _space,
                _rules.Weapons, null));
            ragdoll.gameObject.SetActive(false);
            return ragdoll;
        }

        private float TorsoHalfWidth => _simulation.Ragdoll.TorsoWidthUnits * 0.5f;

        private BoardSnapshot StartingBoardSnapshot()
        {
            var state = new MatchState(
                PerSide<FighterState>.Create(_ => new FighterState(Handedness.Right, _rules.Damage.MaxHp, WeaponIds.Rapier)),
                PerSide<ElectricWallState>.Create(_ => new ElectricWallState()));
            return new BoardSnapshot(state, StartingBoard.Create(state, _rules, _simulation, _aim));
        }

        /// <summary>One turn from this board: locked the way the match locks it (the wall reads the planned move), then simulated.</summary>
        private TurnResult Simulate(BoardSnapshot board, BodyMove left)
        {
            MatchState state = board.State.Clone();
            _wallRules.OnPlansLocked(state.Walls.Left, left, false, _rules.ElectricWall);
            _wallRules.OnPlansLocked(state.Walls.Right, BodyMove.None, false, _rules.ElectricWall);
            var world = new Unity2DPhysicsWorld(_space, new RagdollPrefabSet(_sixBodies, _tenBodies));
            _worlds.Add(world);
            var plans = new PerSide<TurnPlan>(Plan(left), Plan(BodyMove.None));
            return new TurnSimulator(world, _policies).Simulate(new TurnInput(new BoardSnapshot(state, board.Pose.Clone()), plans, _rules, _simulation));
        }

        private static TurnPlan Plan(BodyMove move) => new TurnPlan(WeaponIds.Rapier, move, WeaponPath.Empty, default, true);

        private static List<ElectricWallEvent> WallEvents(TurnResult result, ElectricWallPhase phase) =>
            result.Timeline.Events.OfType<ElectricWallEvent>().Where(e => e.Phase == phase).ToList();

        /// <summary>The raw contacts between the left dummy's body and its own wall.</summary>
        private static List<TurnContact> LeftWallTouches(TurnResult result) =>
            result.Timeline.Events.OfType<ContactEvent>().Select(e => e.Contact)
                .Where(contact => TurnWalls.TryReadTouch(contact.Facts, out Side owner) && owner == Side.Left).ToList();

        private static float PelvisX(TurnResult result, Side side) => result.FinalBoard.Pose.Get(side).Get(BodyPart.Torso).PositionUnits.X;

        private static void Report(string format, params object[] args) =>
            Debug.Log("[XRim wall] " + string.Format(CultureInfo.InvariantCulture, format, args));

        /// <summary>
        /// GDD §13 (Decided): the first backward swipe raises a harmless warning behind the dummy; the second runs it into the wall: electric
        /// damage, a bounce forward, and the wall advances. P1: the dummy ends in front of the advanced wall, ahead of where a plain step back
        /// would have left it.
        /// </summary>
        [UnityTest]
        public IEnumerator TwoRetreatsInARow_ZapAndBounce()
        {
            BoardSnapshot start = StartingBoardSnapshot();
            float startX = start.Pose.Left.Get(BodyPart.Torso).PositionUnits.X;

            TurnResult first = Simulate(start, BodyMove.StepBack);
            ElectricWallEvent warning = WallEvents(first, ElectricWallPhase.Warning).Single();
            float afterFirst = PelvisX(first, Side.Left);
            Report("Turn 1: pelvis {0:0.0} → {1:0.0}; warning raised at {2:0.0} ms at x {3:0.0}.", startX, afterFirst, warning.Time.Milliseconds,
                warning.PositionUnits);

            Assert.That(warning.PositionUnits, Is.LessThan(afterFirst - TorsoHalfWidth), "the warning stands behind the dummy, with a gap");
            Assert.That(first.FinalBoard.State.Fighters.Left.Hp, Is.EqualTo(_rules.Damage.MaxHp), "a warning never hurts");
            Assert.That(first.FinalBoard.Pose.LeftWall.IsUp, Is.True, "it stands on the board for the next planning");

            TurnResult second = Simulate(first.FinalBoard, BodyMove.StepBack);
            ElectricWallEvent zap = WallEvents(second, ElectricWallPhase.Active).Single();
            float afterSecond = PelvisX(second, Side.Left);
            float wallAfter = second.FinalBoard.State.Walls.Left.PositionUnits;
            Report("Turn 2: zap at {0:0.0} ms for {1:0.#} HP ({2} touches); wall {3:0.0} → {4:0.0}; pelvis {5:0.0} → {6:0.0}.", zap.Time.Milliseconds,
                zap.HpDamage, LeftWallTouches(second).Count, warning.PositionUnits, wallAfter, afterFirst, afterSecond);

            Assert.That(LeftWallTouches(second), Is.Not.Empty, "the dummy's body touched the wall");
            Assert.That(zap.HpDamage, Is.EqualTo(_rules.ElectricWall.Damage));
            Assert.That(second.FinalBoard.State.Fighters.Left.Hp, Is.EqualTo(_rules.Damage.MaxHp - _rules.ElectricWall.Damage));
            Assert.That(wallAfter, Is.EqualTo(warning.PositionUnits + _rules.ElectricWall.AdvanceDistanceUnits).Within(Tolerance),
                "the wall moved forward, toward the opponent");
            Assert.That(afterSecond, Is.GreaterThan(wallAfter), "the dummy stands in front of its wall");
            Assert.That(afterSecond, Is.GreaterThan(afterFirst + _rules.FindBodyMove(BodyMove.StepBack).DisplacementUnits.X * 0.5f),
                "bounced: a plain step back would have ended 100 further back");
            yield return null;
        }

        /// <summary>
        /// The wall is part of the board: the frozen board after turn 1 carries it (rules state and pose), and turn 2's physics world rebuilds
        /// it from that snapshot where it stood, solid, so the dummy backs into it. Any other move then takes it away completely.
        /// </summary>
        [UnityTest]
        public IEnumerator TheWall_PersistsAcrossTurns_ThroughTheSnapshot()
        {
            TurnResult first = Simulate(StartingBoardSnapshot(), BodyMove.StepBack);
            BoardSnapshot saved = first.FinalBoard.Clone();
            float raisedX = saved.State.Walls.Left.PositionUnits;

            TurnResult second = Simulate(saved, BodyMove.StepBack);
            Report("Turn 2 from the saved board: wall at {0:0.0}, first frame draws it at {1:0.0}, {2} body touches.", raisedX,
                second.Timeline.Frames[0].Pose.LeftWall.XUnits, LeftWallTouches(second).Count);

            Assert.That(second.Timeline.Frames[0].Pose.LeftWall.IsUp, Is.True);
            Assert.That(second.Timeline.Frames[0].Pose.LeftWall.XUnits, Is.EqualTo(raisedX).Within(Tolerance));
            Assert.That(LeftWallTouches(second), Is.Not.Empty, "rebuilt solid where it stood: the retreat ran into it");

            TurnResult third = Simulate(second.FinalBoard, BodyMove.None);

            Assert.That(LeftWallTouches(third), Is.Empty, "no move: the wall is gone");
            Assert.That(third.Timeline.Frames.All(frame => !frame.Pose.LeftWall.IsUp), Is.True);
            Assert.That(third.FinalBoard.State.Walls.Left.IsUp, Is.False);
            yield return null;
        }
    }
}
