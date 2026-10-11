using System;
using NUnit.Framework;
using XRim.Core;
using XRim.Rules;
using XRim.Rules.Arena;
using XRim.Rules.Match;
using XRim.Rules.Planning;
using XRim.Rules.Settings;
using XRim.Tests.EditMode.Rules.Planning;

namespace XRim.Tests.EditMode.Rules.Match
{
    /// <summary>
    /// The electric wall in the match flow (GDD §13): each side's wall reacts at lock to that side's locked body move and to nothing else.
    /// The simulated turns are stand-ins: a test raises the warning where the simulation would.
    /// </summary>
    public sealed class MatchStateMachineWallTests
    {
        private static readonly PerSide<IWeaponTipSource> Tips = new PerSide<IWeaponTipSource>(
            new FixedWeaponTipSource(PlanningTestData.Tip), new FixedWeaponTipSource(PlanningTestData.Tip));

        private RulesSettings _settings;
        private MatchStateMachine _machine;

        [SetUp]
        public void SetUp()
        {
            _settings = GddStartingValues.CreateRulesSettings();
            _machine = new MatchStateMachine(TestData.CreateMatchSetup(), _settings, new RulePolicies(), new ManualClock(100.0));
            _machine.Start();
        }

        private ElectricWallState WallOf(Side side) => _machine.State.Walls[side];

        /// <summary>Plans the open turn, locks it, and returns each wall as the lock left it (what the simulation is handed).</summary>
        private PerSide<ElectricWallState> Lock(BodyMove left, BodyMove right, Action beforeReady = null)
        {
            _machine.BeginPlanning(Tips);
            if (left != BodyMove.None) _machine.Apply(Side.Left, new SetBodyMoveCommand(left));
            if (right != BodyMove.None) _machine.Apply(Side.Right, new SetBodyMoveCommand(right));
            beforeReady?.Invoke();
            _machine.Apply(Side.Left, new SetReadyCommand(true));
            _machine.Apply(Side.Right, new SetReadyCommand(true));
            Assert.That(_machine.Phase, Is.EqualTo(MatchPhase.Locked));
            return PerSide<ElectricWallState>.Create(side => WallOf(side).Clone());
        }

        /// <summary>Finishes the locked turn the way the simulation would: every warning raised this turn springs up behind its dummy.</summary>
        private void Resolve(Action<MatchState> resolved = null)
        {
            _machine.BeginExecution();
            MatchState state = _machine.State.Clone();
            foreach (Side side in new[] { Side.Left, Side.Right })
            {
                ElectricWallState wall = state.Walls[side];
                if (wall.Phase == ElectricWallPhase.Warning && !wall.IsUp)
                    new ElectricWallRules().RaiseWarning(SimTime.Zero, side, wall, side == Side.Left ? -400f : 400f,
                        new ArenaEdges(-1000f, 1000f, true), _settings.ElectricWall);
            }

            resolved?.Invoke(state);
            _machine.CompleteExecution(new ExecutionReport(state, new PerSide<SimTime?>(null, null), false));
        }

        [Test]
        public void ABackwardSwipe_RaisesTheWarning_AtLock_ForThatSideOnly()
        {
            PerSide<ElectricWallState> locked = Lock(BodyMove.StepBack, BodyMove.Lunge);

            Assert.That(locked.Left.Phase, Is.EqualTo(ElectricWallPhase.Warning));
            Assert.That(locked.Right.Phase, Is.EqualTo(ElectricWallPhase.Inactive));
        }

        [Test]
        public void TheWholeSequence_RunsThroughTheMatchFlow()
        {
            // Right crouches every turn, so nobody idles into a forfeit.
            Lock(BodyMove.StepBack, BodyMove.Crouch);
            Resolve();
            Assert.That(WallOf(Side.Left).IsUp, Is.True, "the warning stands on the board while the next turn is planned");

            Assert.That(Lock(BodyMove.StepBack, BodyMove.Crouch).Left.Phase, Is.EqualTo(ElectricWallPhase.Active), "the second retreat");
            Resolve();

            Assert.That(Lock(BodyMove.StepBack, BodyMove.Crouch).Left.Phase, Is.EqualTo(ElectricWallPhase.Active), "the third");
            Resolve();

            ElectricWallState afterCrouch = Lock(BodyMove.Crouch, BodyMove.Crouch).Left;
            Assert.That(afterCrouch.Phase, Is.EqualTo(ElectricWallPhase.Inactive));
            Assert.That(afterCrouch.IsUp, Is.False);
        }

        [Test]
        public void StandingStillWithTheShield_RaisesNoWall()
        {
            PerSide<ElectricWallState> locked = Lock(BodyMove.None, BodyMove.None, () =>
            {
                _machine.Apply(Side.Left, new SelectWeaponCommand(WeaponIds.Shield));
                _machine.Apply(Side.Left, new SetPathCommand(PlanningTestData.Thrust(60f)));
            });

            Assert.That(locked.Left.Phase, Is.EqualTo(ElectricWallPhase.Inactive));
        }

        [Test]
        public void ARetreatBehindTheShield_StillCounts_TheShieldNeitherTriggersNorStopsIt()
        {
            PerSide<ElectricWallState> locked = Lock(BodyMove.StepBack, BodyMove.None,
                () => _machine.Apply(Side.Left, new SelectWeaponCommand(WeaponIds.Shield)));

            Assert.That(locked.Left.Phase, Is.EqualTo(ElectricWallPhase.Warning));
        }

        [Test]
        public void ADummyKnockedBackWithoutABackwardSwipe_GetsNoWall()
        {
            // Whatever the turn did to the body (a knockback is a pose change the rules never read), only the next locked move counts.
            Lock(BodyMove.None, BodyMove.Lunge);
            Resolve();

            Assert.That(Lock(BodyMove.None, BodyMove.None).Left.Phase, Is.EqualTo(ElectricWallPhase.Inactive));
        }

        [Test]
        public void BothWalls_CanBeActiveInTheSameTurn()
        {
            Lock(BodyMove.StepBack, BodyMove.StepBack);
            Resolve();

            PerSide<ElectricWallState> locked = Lock(BodyMove.StepBack, BodyMove.StepBack);

            Assert.That(locked.Left.Phase, Is.EqualTo(ElectricWallPhase.Active));
            Assert.That(locked.Right.Phase, Is.EqualTo(ElectricWallPhase.Active));
        }

        [Test]
        public void InSuddenDeath_ABackwardSwipeRaisesNoWall_ByDefault()
        {
            Lock(BodyMove.Crouch, BodyMove.Crouch);
            Resolve(state =>
            {
                state.Fighters[Side.Left].Hp = 0f;
                state.Fighters[Side.Right].Hp = 0f;
            });
            Assert.That(_machine.Phase, Is.EqualTo(MatchPhase.SuddenDeathSetup));

            Assert.That(Lock(BodyMove.StepBack, BodyMove.None).Left.Phase, Is.EqualTo(ElectricWallPhase.Inactive));
        }

        [Test]
        public void InSuddenDeath_WallsWork_WhenSwitchedOn()
        {
            _settings.ElectricWall.ActiveInSuddenDeath = true;
            Lock(BodyMove.Crouch, BodyMove.Crouch);
            Resolve(state =>
            {
                state.Fighters[Side.Left].Hp = 0f;
                state.Fighters[Side.Right].Hp = 0f;
            });

            Assert.That(Lock(BodyMove.StepBack, BodyMove.None).Left.Phase, Is.EqualTo(ElectricWallPhase.Warning));
        }
    }
}
