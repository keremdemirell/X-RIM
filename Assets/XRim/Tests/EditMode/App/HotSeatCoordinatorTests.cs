using System.Collections.Generic;
using NUnit.Framework;
using XRim.App;
using XRim.Core;
using XRim.Input;
using XRim.Networking;
using XRim.Rules;
using XRim.Rules.Match;
using XRim.Rules.Planning;
using XRim.Rules.Settings;
using XRim.Simulation;
using XRim.Tests.EditMode.Input;
using XRim.Tests.EditMode.Networking;
using XRim.Tests.EditMode.Rules.Planning;

namespace XRim.Tests.EditMode.App
{
    /// <summary>
    /// Hot-seat (D25, dev mode; timing approved 2026-10-04): the two players plan one after the other through the real authority and
    /// rules, each alone with the full planning time and the real lock-out, behind a cover screen that stops the timer.
    /// </summary>
    public sealed class HotSeatCoordinatorTests
    {
        private const double OpenedSeconds = 40.0;
        private const double Frame = 1.0 / 60.0;

        private ManualClock _clock;
        private RulesSettings _settings;
        private ScriptedTurnSimulator _simulator;
        private LocalTurnAuthority _authority;
        private PerSide<TouchPlanSource> _players;
        private PerSide<StubInputScheme> _schemes;
        private PlanSourceBinder _binder;
        private HotSeatCoordinator _hotSeat;
        private List<PlanningWindow> _windows;
        private int _lockCount;

        private double Planning => _settings.Match.PlanningDurationSeconds;
        private double Lockout => _settings.Match.WeaponSwitchLockoutSeconds;

        [SetUp]
        public void SetUp()
        {
            _clock = new ManualClock(OpenedSeconds);
            _settings = GddStartingValues.CreateRulesSettings();
            _simulator = new ScriptedTurnSimulator();
            // As in the match scene: the next planning phase waits for the client's playback.
            var options = new LocalTurnAuthorityOptions { PlaybackHold = new PlaybackHoldSettings { Mode = PlaybackHoldMode.ClientReport } };
            _authority = new LocalTurnAuthority(TestData.CreateMatchSetup(), _settings, new SimulationSettings(), new RulePolicies(),
                _simulator, _clock, options);
            _windows = new List<PlanningWindow>();
            _lockCount = 0;
            _authority.PlanningStarted += _windows.Add;
            _authority.PlanningLocked += _ => _lockCount++;

            _schemes = PerSide<StubInputScheme>.Create(_ => new StubInputScheme());
            _players = PerSide<TouchPlanSource>.Create(side => new TouchPlanSource(side, _schemes[side],
                new ScreenLayout(SchemeRig.Width, SchemeRig.Height, SchemeRig.BodyZoneFraction, false, side)));
            _hotSeat = new HotSeatCoordinator(_authority, _clock, _players);
            _binder = new PlanSourceBinder(_authority, new IPlanSource[] { _players.Left, _players.Right });
        }

        [TearDown]
        public void TearDown()
        {
            _binder.Dispose();
            _hotSeat.Dispose();
        }

        /// <summary>One frame of the match scene in hot-seat: the coordinator moves the clock, then the authority ticks.</summary>
        private void Frames(double seconds)
        {
            for (double done = 0.0; done < seconds - 1e-9; done += Frame)
            {
                _hotSeat.Advance(Frame);
                _authority.Tick();
            }
        }

        private void AdvanceOnce(double seconds)
        {
            _hotSeat.Advance(seconds);
            _authority.Tick();
        }

        private TouchPlanSource Active => _players[_hotSeat.ActiveSide];

        [Test]
        public void PlanningOpensBehindTheCover_WithTheTimerStopped_AndNobodysInputOn()
        {
            _authority.Start();

            Assert.That(_hotSeat.Stage, Is.EqualTo(HotSeatStage.Cover));
            Assert.That(_hotSeat.ActiveSide, Is.EqualTo(Side.Left), "turn 1: Left first");
            Assert.That(_hotSeat.IsSecondSlot, Is.False);
            Assert.That(_players.Left.IsActive, Is.False);
            Assert.That(_players.Right.IsActive, Is.False);

            Frames(30.0);

            Assert.That(_clock.NowSeconds, Is.EqualTo(OpenedSeconds), "the cover screen stops the planning timer");
            Assert.That(_authority.Phase, Is.EqualTo(MatchPhase.Planning));
        }

        [Test]
        public void ConfirmingTheCover_LetsOnlyThatPlayerPlan_AndRunsTheTimer()
        {
            _authority.Start();
            _hotSeat.ConfirmCover();

            Assert.That(_hotSeat.Stage, Is.EqualTo(HotSeatStage.Planning));
            Assert.That(_hotSeat.ActivePlayer, Is.SameAs(_players.Left));
            Assert.That(_players.Left.IsActive, Is.True);
            Assert.That(_players.Right.IsActive, Is.False);

            AdvanceOnce(2.0);
            Assert.That(_clock.NowSeconds, Is.EqualTo(OpenedSeconds + 2.0));
        }

        [Test]
        public void TheFirstPlayersReady_EndsTheirSlot_AndTheSecondPlayerGetsTheFullTimeFromTheStart()
        {
            _authority.Start();
            _hotSeat.ConfirmCover();
            AdvanceOnce(3.0);
            Assert.That(_players.Left.Send(new SetReadyCommand(true)).Accepted, Is.True);

            AdvanceOnce(Frame);

            Assert.That(_hotSeat.Stage, Is.EqualTo(HotSeatStage.Cover), "the device changes hands");
            Assert.That(_hotSeat.ActiveSide, Is.EqualTo(Side.Right));
            Assert.That(_hotSeat.IsSecondSlot, Is.True);
            Assert.That(_players.Left.IsActive, Is.False);
            Assert.That(_clock.NowSeconds, Is.EqualTo(OpenedSeconds), "the second player's timer starts from when planning opened");

            _hotSeat.ConfirmCover();
            Assert.That(_players.Right.IsActive, Is.True);
            Assert.That(_players.Left.IsActive, Is.False);
            AdvanceOnce(Planning - Lockout - 0.5);
            Assert.That(_players.Right.Send(new SelectWeaponCommand(WeaponIds.Shield)).Accepted, Is.True, "before the lock-out");
            Assert.That(_authority.Phase, Is.EqualTo(MatchPhase.Planning), "the first player's Ready alone does not lock the turn");
        }

        [Test]
        public void TheLockout_AppliesInEachPlayersOwnSlot()
        {
            _authority.Start();
            _hotSeat.ConfirmCover();
            AdvanceOnce(Planning - Lockout + 0.1);
            CommandResult first = _players.Left.Send(new SelectWeaponCommand(WeaponIds.Mace));
            _players.Left.Send(new SetReadyCommand(true));
            AdvanceOnce(Frame);
            _hotSeat.ConfirmCover();
            AdvanceOnce(Planning - Lockout + 0.1);
            CommandResult second = _players.Right.Send(new SelectWeaponCommand(WeaponIds.Shield));

            Assert.That(first.Rejection, Is.EqualTo(CommandRejection.WeaponSwitchLockedOut));
            Assert.That(second.Rejection, Is.EqualTo(CommandRejection.WeaponSwitchLockedOut));
        }

        [Test]
        public void TheFirstPlayersTimeRunningOut_EndsTheirSlot_WithWhatIsSet_AndTheTurnDoesNotLock()
        {
            _authority.Start();
            _hotSeat.ConfirmCover();
            _players.Left.Send(new SetBodyMoveCommand(BodyMove.Lunge));
            int cancelsWhilePlaying = _schemes.Left.CancelCount;

            Frames(Planning + 5.0);

            Assert.That(_authority.Phase, Is.EqualTo(MatchPhase.Planning), "the second player has not planned yet");
            Assert.That(_lockCount, Is.EqualTo(0));
            Assert.That(_hotSeat.Stage, Is.EqualTo(HotSeatStage.Cover));
            Assert.That(_hotSeat.ActiveSide, Is.EqualTo(Side.Right));
            Assert.That(_players.Left.Draft.IsReady, Is.True, "Ready is pressed for them when their time is up");
            Assert.That(_clock.NowSeconds, Is.EqualTo(OpenedSeconds), "stopped behind the cover, turned back for the second player");
            Assert.That(_schemes.Left.CancelCount, Is.EqualTo(cancelsWhilePlaying + 1), "a stroke still being drawn is dropped");

            _hotSeat.ConfirmCover();
            _players.Right.Send(new SetReadyCommand(true));
            AdvanceOnce(Frame);

            Assert.That(_lockCount, Is.EqualTo(1));
            Assert.That(_simulator.Inputs, Has.Count.EqualTo(1));
            Assert.That(_simulator.Inputs[0].Plans[Side.Left].BodyMove, Is.EqualTo(BodyMove.Lunge), "whatever was set executes");
        }

        [Test]
        public void TheFirstSlot_EndsJustBeforeTheDeadline_WhilePlanningIsStillOpen()
        {
            _authority.Start();
            _hotSeat.ConfirmCover();

            AdvanceOnce(Planning - 0.0005);

            Assert.That(_hotSeat.Stage, Is.EqualTo(HotSeatStage.Cover), "the slot ended at the margin");
            Assert.That(_players.Left.Draft.LastRejection, Is.EqualTo(CommandRejection.None), "the Ready for them was accepted");
            Assert.That(_players.Left.Draft.IsReady, Is.True);
        }

        [Test]
        public void TheSecondPlayersTimeRunningOut_LocksTheTurn()
        {
            _authority.Start();
            _hotSeat.ConfirmCover();
            _players.Left.Send(new SetReadyCommand(true));
            AdvanceOnce(Frame);
            _hotSeat.ConfirmCover();

            Frames(Planning - 1.0);
            Assert.That(_lockCount, Is.EqualTo(0));
            Frames(1.5);

            Assert.That(_lockCount, Is.EqualTo(1));
            Assert.That(_hotSeat.Stage, Is.EqualTo(HotSeatStage.Idle));
            Assert.That(_players.Left.IsActive, Is.False);
            Assert.That(_players.Right.IsActive, Is.False);
        }

        [Test]
        public void TheSecondPlayersReady_LocksTheTurnAtOnce()
        {
            _authority.Start();
            _hotSeat.ConfirmCover();
            _players.Left.Send(new SetReadyCommand(true));
            AdvanceOnce(Frame);
            _hotSeat.ConfirmCover();
            AdvanceOnce(1.0);

            _players.Right.Send(new SetReadyCommand(true));

            Assert.That(_lockCount, Is.EqualTo(1));
            Assert.That(_hotSeat.Stage, Is.EqualTo(HotSeatStage.Idle));
            Assert.That(_hotSeat.ActivePlayer, Is.Null);
        }

        [Test]
        public void TheFirstPlanner_AlternatesEachTurn()
        {
            _authority.Start();
            var firsts = new List<Side>();
            for (int turn = 0; turn < 4; turn++)
            {
                firsts.Add(_hotSeat.ActiveSide);
                for (int slot = 0; slot < 2; slot++)
                {
                    _hotSeat.ConfirmCover();
                    Active.Send(new SetBodyMoveCommand(BodyMove.Crouch)); // a move, so nobody forfeits
                    Active.Send(new SetReadyCommand(true));
                    if (slot == 0) AdvanceOnce(Frame);
                }

                AdvanceOnce(Frame); // resolves the turn
                _authority.NotifyPlaybackFinished(turn);
                AdvanceOnce(Frame); // the next planning opens
            }

            Assert.That(firsts, Is.EqualTo(new[] { Side.Left, Side.Right, Side.Left, Side.Right }));
            Assert.That(_windows, Has.Count.EqualTo(5));
        }

        [Test]
        public void TheClockRuns_BetweenPlanningPhases()
        {
            _authority.Start();
            _hotSeat.ConfirmCover();
            _players.Left.Send(new SetReadyCommand(true));
            AdvanceOnce(Frame);
            _hotSeat.ConfirmCover();
            _players.Right.Send(new SetReadyCommand(true));
            double locked = _clock.NowSeconds;

            _hotSeat.Advance(0.25);

            Assert.That(_clock.NowSeconds, Is.EqualTo(locked + 0.25));
        }

        [Test]
        public void ASideWithLessPlanningTime_PlansFirst()
        {
            PlanningWindow even = Window(0, 12f, 6f);
            PlanningWindow odd = Window(1, 6f, 12f);

            Assert.That(HotSeatCoordinator.FirstPlanner(even), Is.EqualTo(Side.Right), "a stunned Right plans first on an even turn");
            Assert.That(HotSeatCoordinator.FirstPlanner(odd), Is.EqualTo(Side.Left));
            Assert.That(HotSeatCoordinator.FirstPlanner(Window(0, 12f, 12f)), Is.EqualTo(Side.Left));
            Assert.That(HotSeatCoordinator.FirstPlanner(Window(1, 12f, 12f)), Is.EqualTo(Side.Right));
        }

        [Test]
        public void Dispose_StopsFollowingTheAuthority()
        {
            _hotSeat.Dispose();

            _authority.Start();

            Assert.That(_hotSeat.Stage, Is.EqualTo(HotSeatStage.Idle));
        }

        private PlanningWindow Window(int turnIndex, float leftSeconds, float rightSeconds)
        {
            var constraints = PerSide<PlanningConstraints>.Create(_ => PlanningConstraints.CreateDefault(_settings.Match));
            constraints.Left.PlanningDurationSeconds = leftSeconds;
            constraints.Right.PlanningDurationSeconds = rightSeconds;
            return new PlanningWindow(turnIndex, OpenedSeconds + 12.0, constraints, false, TestData.CreateInitialBoard(_settings),
                PerSide<IWeaponTipSource>.Create(_ => new FixedWeaponTipSource(new Vec2(100f, 100f))));
        }
    }
}
