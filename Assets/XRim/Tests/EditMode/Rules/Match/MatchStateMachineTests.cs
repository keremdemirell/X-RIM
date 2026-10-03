using System;
using NUnit.Framework;
using XRim.Core;
using XRim.Rules;
using XRim.Rules.Limbs;
using XRim.Rules.Match;
using XRim.Rules.Planning;
using XRim.Rules.Settings;
using XRim.Rules.Status;
using XRim.Tests.EditMode.Rules.Planning;

namespace XRim.Tests.EditMode.Rules.Match
{
    /// <summary>The match flow: phases, timers, locking, resolving and the hand-over to sudden death (GDD §3, §13, §14).</summary>
    public sealed class MatchStateMachineTests
    {
        private const double StartSeconds = 100.0;
        private const double DeadlineSeconds = StartSeconds + 12.0;
        private const double JustBefore = 0.001;

        private static readonly PerSide<IWeaponTipSource> Tips = new PerSide<IWeaponTipSource>(
            new FixedWeaponTipSource(PlanningTestData.Tip), new FixedWeaponTipSource(PlanningTestData.Tip));

        private ManualClock _clock;
        private RulesSettings _settings;
        private RulePolicies _policies;
        private MatchStateMachine _machine;

        [SetUp]
        public void SetUp()
        {
            _clock = new ManualClock(StartSeconds);
            _settings = GddStartingValues.CreateRulesSettings();
            _policies = new RulePolicies();
            _machine = new MatchStateMachine(TestData.CreateMatchSetup(), _settings, _policies, _clock);
        }

        private void StartPlanning()
        {
            _machine.Start();
            _machine.BeginPlanning(Tips);
        }

        private void ReadyBoth()
        {
            _machine.Apply(Side.Left, new SetReadyCommand(true));
            _machine.Apply(Side.Right, new SetReadyCommand(true));
        }

        /// <summary>The report a simulator would return: the current board, changed the way the test wants.</summary>
        private ExecutionReport Report(Action<MatchState> tweak = null)
        {
            MatchState state = _machine.State.Clone();
            tweak?.Invoke(state);
            return new ExecutionReport(state, new PerSide<SimTime?>(null, null), false);
        }

        /// <summary>
        /// Plays one whole turn through the public flow. By default both sides make a body move, so nobody idles.
        /// </summary>
        private EndCheckResult PlayTurn(Action<MatchState> tweak = null, bool leftIdle = false, bool rightIdle = false)
        {
            _machine.BeginPlanning(Tips);
            if (!leftIdle) _machine.Apply(Side.Left, new SetBodyMoveCommand(BodyMove.Crouch));
            if (!rightIdle) _machine.Apply(Side.Right, new SetBodyMoveCommand(BodyMove.Crouch));
            ReadyBoth();
            _machine.BeginExecution();
            return _machine.CompleteExecution(Report(tweak));
        }

        private static void Kill(MatchState state, Side side) => state.Fighters[side].Hp = 0f;

        // --- The phases ------------------------------------------------------------------------

        [Test]
        public void ANewMatch_WaitsInMatchSetup_WithBothLoadoutsPublic()
        {
            Assert.That(_machine.Phase, Is.EqualTo(MatchPhase.MatchSetup));
            Assert.That(_machine.Setup.Fighters[Side.Left].Loadout, Is.EqualTo(new[] { WeaponIds.Rapier, WeaponIds.Mace, WeaponIds.Shield }));
            Assert.That(_machine.Setup.Fighters[Side.Right].Loadout, Is.EqualTo(new[] { WeaponIds.Mace, WeaponIds.Spear, WeaponIds.Shield }));
            Assert.That(_machine.Sessions, Is.Null);
        }

        [Test]
        public void Start_BeginsTurnOne_WithConstraintsReady()
        {
            _machine.Start();

            Assert.That(_machine.Phase, Is.EqualTo(MatchPhase.TurnStart));
            Assert.That(_machine.State.TurnIndex, Is.EqualTo(0));
            Assert.That(_machine.Constraints[Side.Left].PlanningDurationSeconds, Is.EqualTo(12f));
            Assert.That(_machine.Sessions, Is.Null, "the timer has not started: the authority begins planning");
        }

        [Test]
        public void BeginPlanning_OpensASessionPerSide_AndStartsTheTimerNow()
        {
            _machine.Start();
            _clock.Advance(30.0); // time spent waiting at TurnStart (for example for playback) is not planning time

            _machine.BeginPlanning(Tips);

            Assert.That(_machine.Phase, Is.EqualTo(MatchPhase.Planning));
            Assert.That(_machine.Sessions[Side.Left].Weapon, Is.EqualTo(WeaponIds.Rapier));
            Assert.That(_machine.Sessions[Side.Right].Weapon, Is.EqualTo(WeaponIds.Mace));
            Assert.That(_machine.Sessions[Side.Left].DeadlineSeconds, Is.EqualTo(StartSeconds + 30.0 + 12.0));
            Assert.That(_machine.PlanningDeadlineSeconds, Is.EqualTo(StartSeconds + 30.0 + 12.0));
        }

        [Test]
        public void TheFullCycle_GoesThroughEveryPhaseInOrder()
        {
            Assert.That(_machine.Phase, Is.EqualTo(MatchPhase.MatchSetup));
            _machine.Start();
            Assert.That(_machine.Phase, Is.EqualTo(MatchPhase.TurnStart));
            _machine.BeginPlanning(Tips);
            Assert.That(_machine.Phase, Is.EqualTo(MatchPhase.Planning));
            ReadyBoth();
            Assert.That(_machine.Phase, Is.EqualTo(MatchPhase.Locked));
            _machine.BeginExecution();
            Assert.That(_machine.Phase, Is.EqualTo(MatchPhase.Executing));

            EndCheckResult result = _machine.CompleteExecution(Report());

            Assert.That(result.Resolution, Is.EqualTo(TurnResolution.NextTurn));
            Assert.That(_machine.Phase, Is.EqualTo(MatchPhase.TurnStart));
            Assert.That(_machine.State.TurnIndex, Is.EqualTo(1));
        }

        [Test]
        public void CallsInTheWrongPhase_AreProgrammerErrors()
        {
            Assert.Throws<InvalidOperationException>(() => _machine.BeginPlanning(Tips), "before Start");
            Assert.Throws<InvalidOperationException>(() => _machine.BeginExecution());
            Assert.Throws<InvalidOperationException>(() => _machine.CompleteExecution(Report()));

            _machine.Start();
            Assert.Throws<InvalidOperationException>(() => _machine.Start(), "twice");

            _machine.BeginPlanning(Tips);
            Assert.Throws<InvalidOperationException>(() => _machine.BeginPlanning(Tips), "already planning");
            Assert.Throws<InvalidOperationException>(() => _machine.BeginExecution(), "not locked yet");
            Assert.Throws<InvalidOperationException>(() => _machine.CompleteExecution(Report()), "not executing");
        }

        [Test]
        public void Start_RefusesABrokenLoadout()
        {
            var setup = new MatchSetup(
                new PerSide<FighterSetup>(
                    new FighterSetup(Handedness.Right, new[] { WeaponIds.Rapier, WeaponIds.Mace }),
                    new FighterSetup(Handedness.Left, new[] { WeaponIds.Mace, WeaponIds.Spear, WeaponIds.Shield })),
                TestData.Seed);
            var machine = new MatchStateMachine(setup, _settings, _policies, _clock);

            var error = Assert.Throws<InvalidOperationException>(() => machine.Start());

            Assert.That(error.Message, Does.Contain("Left"));
            Assert.That(machine.Phase, Is.EqualTo(MatchPhase.MatchSetup));
        }

        // --- Planning ends: both Ready, or the timers (§3, Decided) ----------------------------

        [Test]
        public void OneReady_DoesNotLock_ButTheSecondReadyLocksAtOnce()
        {
            StartPlanning();

            _machine.Apply(Side.Left, new SetReadyCommand(true));
            Assert.That(_machine.Phase, Is.EqualTo(MatchPhase.Planning));

            _machine.Apply(Side.Right, new SetReadyCommand(true));
            Assert.That(_machine.Phase, Is.EqualTo(MatchPhase.Locked), "as soon as both are Ready");
        }

        [Test]
        public void TheTimer_LocksAtTheDeadline_NotBefore()
        {
            StartPlanning();

            _clock.Set(DeadlineSeconds - JustBefore);
            _machine.Tick();
            Assert.That(_machine.Phase, Is.EqualTo(MatchPhase.Planning));

            _clock.Set(DeadlineSeconds);
            _machine.Tick();
            Assert.That(_machine.Phase, Is.EqualTo(MatchPhase.Locked));
        }

        [Test]
        public void OneReady_AndTheTimerEnding_Locks()
        {
            StartPlanning();
            _machine.Apply(Side.Left, new SetReadyCommand(true));

            _clock.Set(DeadlineSeconds);
            _machine.Tick();

            Assert.That(_machine.Phase, Is.EqualTo(MatchPhase.Locked));
            Assert.That(_machine.LockedPlans[Side.Left].PressedReady, Is.True);
            Assert.That(_machine.LockedPlans[Side.Right].PressedReady, Is.False);
        }

        [Test]
        public void Timeout_WhateverIsSetStillExecutes()
        {
            StartPlanning();
            _machine.Apply(Side.Left, new SelectWeaponCommand(WeaponIds.Mace));
            _machine.Apply(Side.Left, new SetBodyMoveCommand(BodyMove.Lunge));
            _machine.Apply(Side.Left, new SetPathCommand(PlanningTestData.Thrust(250f)));
            _machine.Apply(Side.Right, new SetBodyMoveCommand(BodyMove.Jump));

            _clock.Set(DeadlineSeconds);
            _machine.Tick();

            PerSide<TurnPlan> plans = _machine.BeginExecution();
            Assert.That(plans[Side.Left].Weapon, Is.EqualTo(WeaponIds.Mace));
            Assert.That(plans[Side.Left].BodyMove, Is.EqualTo(BodyMove.Lunge));
            Assert.That(plans[Side.Left].Path.IsEmpty, Is.False);
            Assert.That(plans[Side.Right].BodyMove, Is.EqualTo(BodyMove.Jump));
        }

        [Test]
        public void ANothingPlan_RunsAsAnIdleTurn()
        {
            StartPlanning();

            _clock.Set(DeadlineSeconds);
            _machine.Tick();

            Assert.That(_machine.LockedPlans[Side.Left].BodyMove, Is.EqualTo(BodyMove.None));
            Assert.That(_machine.LockedPlans[Side.Left].Path.IsEmpty, Is.True);
            Assert.That(_machine.LockedPlans[Side.Left].Weapon, Is.EqualTo(WeaponIds.Rapier));
        }

        [Test]
        public void ASideWithAShorterTimer_WaitsForTheOther()
        {
            _machine.Start();
            _machine.Constraints[Side.Left].PlanningDurationSeconds = 6f; // for example a stun (Session 06)
            _machine.BeginPlanning(Tips);
            Assert.That(_machine.PlanningDeadlineSeconds, Is.EqualTo(DeadlineSeconds));

            _clock.Set(StartSeconds + 6.0);
            _machine.Tick();
            Assert.That(_machine.Phase, Is.EqualTo(MatchPhase.Planning), "the right side still has time");
            Assert.That(_machine.Apply(Side.Left, new SetBodyMoveCommand(BodyMove.Crouch)).Rejection,
                Is.EqualTo(CommandRejection.NotInPlanningPhase), "but the left side is closed");

            _machine.Apply(Side.Right, new SetReadyCommand(true));
            Assert.That(_machine.Phase, Is.EqualTo(MatchPhase.Locked));
        }

        // --- Commands --------------------------------------------------------------------------

        [Test]
        public void Commands_AreRoutedToTheirOwnSide()
        {
            StartPlanning();

            _machine.Apply(Side.Left, new SelectWeaponCommand(WeaponIds.Shield));

            Assert.That(_machine.Sessions[Side.Left].Weapon, Is.EqualTo(WeaponIds.Shield));
            Assert.That(_machine.Sessions[Side.Right].Weapon, Is.EqualTo(WeaponIds.Mace));
        }

        [Test]
        public void Commands_UseTheMachinesClock_ForTheLockout()
        {
            StartPlanning();

            _clock.Set(DeadlineSeconds - 1.5 - JustBefore);
            Assert.That(_machine.Apply(Side.Left, new SelectWeaponCommand(WeaponIds.Mace)).Accepted, Is.True);

            _clock.Set(DeadlineSeconds - 1.5);
            Assert.That(_machine.Apply(Side.Left, new SelectWeaponCommand(WeaponIds.Shield)).Rejection,
                Is.EqualTo(CommandRejection.WeaponSwitchLockedOut));
        }

        [Test]
        public void Commands_OutsidePlanning_AreRefused()
        {
            Assert.That(_machine.Apply(Side.Left, new SetReadyCommand(true)).Rejection,
                Is.EqualTo(CommandRejection.NotInPlanningPhase), "before the match starts");

            StartPlanning();
            ReadyBoth();
            Assert.That(_machine.Apply(Side.Left, new SetReadyCommand(false)).Rejection,
                Is.EqualTo(CommandRejection.NotInPlanningPhase), "plans are locked: Ready can no longer be cancelled");
        }

        [Test]
        public void ReadyCanBeCancelled_UntilTheOpponentIsReadyToo()
        {
            StartPlanning();
            _machine.Apply(Side.Left, new SetReadyCommand(true));

            Assert.That(_machine.Apply(Side.Left, new SetReadyCommand(false)).Accepted, Is.True);

            Assert.That(_machine.Phase, Is.EqualTo(MatchPhase.Planning));
            Assert.That(_machine.PublicStateOf(Side.Left).IsReady, Is.False);
        }

        [Test]
        public void ThePublicState_ShowsOneSidesWeaponAndReady()
        {
            StartPlanning();

            _machine.Apply(Side.Left, new SelectWeaponCommand(WeaponIds.Shield));
            _machine.Apply(Side.Left, new SetReadyCommand(true));

            Assert.That(_machine.PublicStateOf(Side.Left), Is.EqualTo(new PublicPlanningState(WeaponIds.Shield, true, false)));
            Assert.That(_machine.PublicStateOf(Side.Right), Is.EqualTo(new PublicPlanningState(WeaponIds.Mace, false, false)));
        }

        // --- Locking ---------------------------------------------------------------------------

        [Test]
        public void AWeaponSwitch_BecomesTheFightersWeapon_WhenPlansLock()
        {
            StartPlanning();
            _machine.Apply(Side.Left, new SelectWeaponCommand(WeaponIds.Mace));
            Assert.That(_machine.State.Fighters[Side.Left].CurrentWeapon, Is.EqualTo(WeaponIds.Rapier), "still the old board");

            ReadyBoth();

            Assert.That(_machine.State.Fighters[Side.Left].CurrentWeapon, Is.EqualTo(WeaponIds.Mace));
            Assert.That(_machine.LockedPlans[Side.Left].Weapon, Is.EqualTo(WeaponIds.Mace));
        }

        [Test]
        public void APlanThatFailsValidation_RunsAsAnIdlePlan_WithTheSameWeapon()
        {
            StartPlanning();
            _machine.Apply(Side.Left, new SelectWeaponCommand(WeaponIds.Mace));
            _machine.Apply(Side.Left, new SetBodyMoveCommand(BodyMove.Lunge));
            _machine.Apply(Side.Right, new SetBodyMoveCommand(BodyMove.Jump));
            _machine.Constraints[Side.Left].ForbidBodyMove(BodyMove.Lunge); // a limit that arrived after the move was set

            ReadyBoth();

            Assert.That(_machine.LockValidation[Side.Left].Rejection, Is.EqualTo(CommandRejection.BodyMoveNotAllowed));
            Assert.That(_machine.LockedPlans[Side.Left].BodyMove, Is.EqualTo(BodyMove.None));
            Assert.That(_machine.LockedPlans[Side.Left].Weapon, Is.EqualTo(WeaponIds.Mace), "the switch was public, so it stays");
            Assert.That(_machine.LockValidation[Side.Right].Accepted, Is.True);
            Assert.That(_machine.LockedPlans[Side.Right].BodyMove, Is.EqualTo(BodyMove.Jump), "the other side is untouched");
        }

        [Test]
        public void StatusEffects_ShapeTheNextTurnsConstraints()
        {
            _machine.State.Fighters[Side.Left].Statuses.Add(new NoJumpStatus());
            _machine.Start();
            _machine.BeginPlanning(Tips);

            Assert.That(_machine.Constraints[Side.Left].IsBodyMoveAllowed(BodyMove.Jump), Is.False);
            Assert.That(_machine.Constraints[Side.Right].IsBodyMoveAllowed(BodyMove.Jump), Is.True);
            Assert.That(_machine.Apply(Side.Left, new SetBodyMoveCommand(BodyMove.Jump)).Rejection,
                Is.EqualTo(CommandRejection.BodyMoveNotAllowed));
        }

        [Test]
        public void MobilityPenalty_ByDefaultForbidsNothing()
        {
            _machine.State.Fighters[Side.Left].MarkSevered(BodyPart.LeftLeg);
            StartPlanning();

            Assert.That(_policies.MobilityPenalty, Is.InstanceOf<NoMobilityPenaltyPolicy>(), "§12 penalty is TBD until Session 11");
            foreach (BodyMove move in new[] { BodyMove.Crouch, BodyMove.Lunge, BodyMove.StepBack, BodyMove.Jump })
            {
                Assert.That(_machine.Constraints[Side.Left].IsBodyMoveAllowed(move), Is.True, move.ToString());
            }
        }

        [Test]
        public void MobilityPenalty_ShapesEachTurnsConstraints()
        {
            _policies.MobilityPenalty = new NoJumpWithoutBothLegsPolicy();
            _machine.State.Fighters[Side.Left].MarkSevered(BodyPart.LeftLeg);
            StartPlanning();

            Assert.That(_machine.Constraints[Side.Left].IsBodyMoveAllowed(BodyMove.Jump), Is.False);
            Assert.That(_machine.Constraints[Side.Right].IsBodyMoveAllowed(BodyMove.Jump), Is.True, "the right dummy has both legs");
        }

        /// <summary>One of the §12 options, for the seam only: no jump on one leg.</summary>
        private sealed class NoJumpWithoutBothLegsPolicy : IMobilityPenaltyPolicy
        {
            public void Apply(FighterState fighter, PlanningConstraints constraints)
            {
                if (fighter.IsSevered(BodyPart.LeftLeg) || fighter.IsSevered(BodyPart.RightLeg)) constraints.ForbidBodyMove(BodyMove.Jump);
            }
        }

        // --- Resolving -------------------------------------------------------------------------

        [Test]
        public void CompleteExecution_AdoptsTheSimulatedState_AsACopy()
        {
            StartPlanning();
            ReadyBoth();
            _machine.BeginExecution();
            ExecutionReport report = Report(state => state.Fighters[Side.Left].Hp = 55f);

            _machine.CompleteExecution(report);
            report.ResolvedState.Fighters[Side.Left].Hp = 1f;

            Assert.That(_machine.State.Fighters[Side.Left].Hp, Is.EqualTo(55f));
        }

        [Test]
        public void CompleteExecution_RefusesAReportForAnotherTurn()
        {
            StartPlanning();
            ReadyBoth();
            _machine.BeginExecution();

            Assert.Throws<ArgumentException>(() => _machine.CompleteExecution(Report(state => state.TurnIndex = 5)));
            Assert.That(_machine.Phase, Is.EqualTo(MatchPhase.Executing), "nothing changed");
        }

        [Test]
        public void IdleTurns_AreCounted_AndAMoveResetsTheCount()
        {
            _machine.Start();

            PlayTurn(leftIdle: true);
            PlayTurn(leftIdle: true);
            Assert.That(_machine.State.Fighters[Side.Left].ConsecutiveIdleTurns, Is.EqualTo(2));
            Assert.That(_machine.State.Fighters[Side.Right].ConsecutiveIdleTurns, Is.EqualTo(0));

            PlayTurn();
            Assert.That(_machine.State.Fighters[Side.Left].ConsecutiveIdleTurns, Is.EqualTo(0));
        }

        [Test]
        public void ThreeIdleTurnsInARow_LoseByForfeit()
        {
            _machine.Start();
            Assert.That(PlayTurn(leftIdle: true).Resolution, Is.EqualTo(TurnResolution.NextTurn));
            Assert.That(PlayTurn(leftIdle: true).Resolution, Is.EqualTo(TurnResolution.NextTurn));

            EndCheckResult result = PlayTurn(leftIdle: true);

            Assert.That(result.Resolution, Is.EqualTo(TurnResolution.MatchOver));
            Assert.That(_machine.Phase, Is.EqualTo(MatchPhase.MatchOver));
            Assert.That(_machine.Outcome.Winner, Is.EqualTo(Side.Right));
            Assert.That(_machine.Outcome.Reason, Is.EqualTo(MatchEndReason.Forfeit));
            Assert.That(_machine.Outcome.TurnCount, Is.EqualTo(3));
        }

        [Test]
        public void WhatCountsAsIdle_IsThePolicysCall()
        {
            _policies.IdleTurn = new EveryTurnIsIdlePolicy();
            _machine.Start();
            PlayTurn();
            PlayTurn();

            EndCheckResult result = PlayTurn();

            Assert.That(result.Resolution, Is.EqualTo(TurnResolution.EnterSuddenDeath), "both idle at once: the both-forfeit rule");
        }

        // --- KO, sudden death, turn cap --------------------------------------------------------

        [Test]
        public void AKo_EndsTheMatch_AndNothingMovesAfterIt()
        {
            _machine.Start();

            EndCheckResult result = PlayTurn(state => Kill(state, Side.Right));

            Assert.That(result.Resolution, Is.EqualTo(TurnResolution.MatchOver));
            Assert.That(_machine.Phase, Is.EqualTo(MatchPhase.MatchOver));
            Assert.That(_machine.Outcome.Winner, Is.EqualTo(Side.Left));
            Assert.That(_machine.Outcome.Reason, Is.EqualTo(MatchEndReason.KnockOut));
            Assert.That(_machine.Apply(Side.Left, new SetReadyCommand(true)).Rejection, Is.EqualTo(CommandRejection.NotInPlanningPhase));
            Assert.Throws<InvalidOperationException>(() => _machine.BeginPlanning(Tips));
            _machine.Tick(); // harmless
        }

        [Test]
        public void ADoubleKo_GoesToSuddenDeathSetup_WithBothAtOneHp()
        {
            _machine.Start();

            EndCheckResult result = PlayTurn(state =>
            {
                Kill(state, Side.Left);
                Kill(state, Side.Right);
            });

            Assert.That(result.Resolution, Is.EqualTo(TurnResolution.EnterSuddenDeath));
            Assert.That(_machine.Phase, Is.EqualTo(MatchPhase.SuddenDeathSetup));
            Assert.That(_machine.Outcome, Is.Null);
            Assert.That(_machine.State.IsSuddenDeath, Is.True);
            Assert.That(_machine.State.TurnIndex, Is.EqualTo(1));
            Assert.That(_machine.State.Fighters[Side.Left].Hp, Is.EqualTo(RuleConstants.SuddenDeathHp));
            Assert.That(_machine.State.Fighters[Side.Right].Hp, Is.EqualTo(RuleConstants.SuddenDeathHp));
        }

        [Test]
        public void FromSuddenDeathSetup_PlanningBeginsAgain_AndTheFirstHitWins()
        {
            _machine.Start();
            PlayTurn(state =>
            {
                Kill(state, Side.Left);
                Kill(state, Side.Right);
            });

            EndCheckResult result = PlayTurn(state => Kill(state, Side.Left));

            Assert.That(result.Resolution, Is.EqualTo(TurnResolution.MatchOver));
            Assert.That(_machine.Outcome.Winner, Is.EqualTo(Side.Right));
            Assert.That(_machine.Outcome.Reason, Is.EqualTo(MatchEndReason.SuddenDeathHit));
        }

        [Test]
        public void ASuddenDeathTurnWithoutAHit_Repeats()
        {
            _machine.Start();
            PlayTurn(state =>
            {
                Kill(state, Side.Left);
                Kill(state, Side.Right);
            });

            EndCheckResult result = PlayTurn();

            Assert.That(result.Resolution, Is.EqualTo(TurnResolution.RepeatSuddenDeathTurn), "Session 12 seam");
            Assert.That(_machine.Phase, Is.EqualTo(MatchPhase.TurnStart));
            Assert.That(_machine.State.IsSuddenDeath, Is.True);
            Assert.That(_machine.State.TurnIndex, Is.EqualTo(2));
        }

        [Test]
        public void AStalledMatch_ReachesSuddenDeathSetup_AfterTurn30()
        {
            _machine.Start();

            for (int turn = 1; turn < _settings.Match.TurnCap; turn++)
            {
                Assert.That(PlayTurn().Resolution, Is.EqualTo(TurnResolution.NextTurn), $"turn {turn}");
            }

            EndCheckResult result = PlayTurn();

            Assert.That(result.Resolution, Is.EqualTo(TurnResolution.EnterSuddenDeath));
            Assert.That(_machine.Phase, Is.EqualTo(MatchPhase.SuddenDeathSetup));
            Assert.That(_machine.State.TurnIndex, Is.EqualTo(30));
            Assert.That(_machine.State.Fighters[Side.Left].Hp, Is.EqualTo(RuleConstants.SuddenDeathHp), "both set to 1 HP, whatever they had");
        }

        // --- Test doubles ----------------------------------------------------------------------

        private sealed class NoJumpStatus : IStatusEffect
        {
            public StatusKind Kind => StatusKind.Stunned;
            public int RemainingTurns => 1;

            public void ApplyToNextTurn(PlanningConstraints constraints) => constraints.ForbidBodyMove(BodyMove.Jump);
        }

        private sealed class EveryTurnIsIdlePolicy : IIdleTurnPolicy
        {
            public bool IsIdle(TurnPlan plan) => true;
        }
    }
}
