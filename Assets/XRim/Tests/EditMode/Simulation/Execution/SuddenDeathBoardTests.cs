using NUnit.Framework;
using XRim.Core;
using XRim.Rules;
using XRim.Rules.Arena;
using XRim.Rules.Match;
using XRim.Rules.Settings;
using XRim.Rules.SuddenDeath;
using XRim.Simulation;
using XRim.Simulation.Drivers;
using XRim.Simulation.Execution;
using XRim.Simulation.Physics;

namespace XRim.Tests.EditMode.Simulation.Execution
{
    /// <summary>
    /// The pose half of sudden death's setup (GDD §14; the board mode P5, designer 2026-10-11): the board carries over by default, a reset
    /// puts both dummies on their start lines, and the walls are drawn as the rules state has them either way.
    /// </summary>
    public sealed class SuddenDeathBoardTests
    {
        private readonly AimFromShoulderModel _aim = new AimFromShoulderModel();
        private RulesSettings _rules;
        private SimulationSettings _simulation;
        private MatchState _state;
        private PoseSnapshot _ended;

        [SetUp]
        public void SetUp()
        {
            _rules = GddStartingValues.CreateRulesSettings();
            _simulation = new SimulationSettings();
            _state = MatchState.CreateInitial(TestData.CreateMatchSetup(), _rules);
            _ended = new PoseSnapshot();
            _ended.Left.Set(BodyPart.Torso, new BodyPose(new Vec2(-120f, 40f), 75f));
            _ended.Left.IsTorsoSplit = true;
            _ended.Right.Set(BodyPart.Torso, new BodyPose(new Vec2(90f, 35f), -80f));
            _ended.SeveredLimbs.Add(new SeveredLimbPose(Side.Right, BodyPart.LeftLeg, new BodyPose(new Vec2(200f, 10f), 90f)));
            _ended.LeftWall = new ElectricWallPose(ElectricWallPhase.Active, -400f);
        }

        private PoseSnapshot Prepare(PoseSnapshot startPose = null) =>
            SuddenDeathBoard.Prepare(_ended, _state, _rules, _simulation, _aim, startPose);

        private static float TorsoX(FighterPose fighter) => fighter.Get(BodyPart.Torso).PositionUnits.X;

        [Test]
        public void ByDefault_TheBoardCarriesOver_FloorLimbsIncluded()
        {
            PoseSnapshot pose = Prepare();

            Assert.That(pose, Is.Not.SameAs(_ended));
            Assert.That(TorsoX(pose.Left), Is.EqualTo(-120f));
            Assert.That(TorsoX(pose.Right), Is.EqualTo(90f));
            Assert.That(pose.SeveredLimbs, Has.Count.EqualTo(1), "a limb on the floor is still a club in sudden death");
            Assert.That(pose.Left.IsTorsoSplit, Is.True, "the split torso is rebuilt whole when the turn loads (S5)");
        }

        [Test]
        public void TheWalls_AreDrawnAsTheStateHasThem()
        {
            Assert.That(Prepare().LeftWall.IsUp, Is.False, "the setup took the walls down (off in sudden death)");

            _state.Walls[Side.Left].Phase = ElectricWallPhase.Active;
            _state.Walls[Side.Left].IsUp = true;
            _state.Walls[Side.Left].PositionUnits = -400f;

            ElectricWallPose wall = Prepare().LeftWall;
            Assert.That((wall.IsUp, wall.Phase, wall.XUnits), Is.EqualTo((true, ElectricWallPhase.Active, -400f)), "walls switched on stay");
        }

        [Test]
        public void AReset_PutsBothDummiesWholeOnTheirStartLines_WithNoFloorLimbsOrWalls()
        {
            _rules.SuddenDeath.BoardMode = SuddenDeathBoardMode.ResetToMatchStart;

            PoseSnapshot pose = Prepare();

            PoseSnapshot start = StartingBoard.Create(_state, _rules, _simulation, _aim);
            Assert.That(TorsoX(pose.Left), Is.EqualTo(-_rules.Arena.StartingGapUnits * 0.5f));
            Assert.That(TorsoX(pose.Right), Is.EqualTo(TorsoX(start.Right)));
            Assert.That(pose.Left.IsTorsoSplit, Is.False);
            Assert.That(pose.SeveredLimbs, Is.Empty);
            Assert.That(pose.LeftWall.IsUp, Is.False);
        }

        [Test]
        public void AReset_UsesTheMatchsOwnStartPose_WhenItWasGiven()
        {
            _rules.SuddenDeath.BoardMode = SuddenDeathBoardMode.ResetToMatchStart;
            var given = new PoseSnapshot();
            given.Left.Set(BodyPart.Torso, new BodyPose(new Vec2(-777f, 180f), 0f));

            PoseSnapshot pose = Prepare(given);

            Assert.That(TorsoX(pose.Left), Is.EqualTo(-777f));
            Assert.That(pose, Is.Not.SameAs(given));
        }
    }
}
