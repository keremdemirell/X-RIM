using System;
using NUnit.Framework;
using XRim.Core;
using XRim.Rules;
using XRim.Rules.Match;
using XRim.Rules.Planning;
using XRim.Rules.Settings;
using XRim.Tests.EditMode.Rules.Planning;

namespace XRim.Tests.EditMode.Rules.Match
{
    /// <summary>
    /// Sudden death in the match flow (GDD §14; designer's choices of 2026-10-11): the setup on entry, the options as planning limits, a turn
    /// without a winner played again at 1 HP and counted, the death times reaching the evaluator, and sudden death always ending.
    /// </summary>
    public sealed class MatchStateMachineSuddenDeathTests
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

        /// <summary>Plays the open turn (both crouch, so nobody idles) with the simulated result the test wants.</summary>
        private EndCheckResult PlayTurn(Action<MatchState> resolved = null, PerSide<SimTime?> hits = null, PerSide<SimTime?> deaths = null)
        {
            _machine.BeginPlanning(Tips);
            _machine.Apply(Side.Left, new SetBodyMoveCommand(BodyMove.Crouch));
            _machine.Apply(Side.Right, new SetBodyMoveCommand(BodyMove.Crouch));
            _machine.Apply(Side.Left, new SetReadyCommand(true));
            _machine.Apply(Side.Right, new SetReadyCommand(true));
            _machine.BeginExecution();
            MatchState state = _machine.State.Clone();
            resolved?.Invoke(state);
            return _machine.CompleteExecution(new ExecutionReport(state, hits ?? new PerSide<SimTime?>(null, null), false, deaths));
        }

        private void EnterByDoubleKo() => PlayTurn(state =>
        {
            state.Fighters[Side.Left].Hp = 0f;
            state.Fighters[Side.Right].Hp = 0f;
        });

        [Test]
        public void EnteringSuddenDeath_AppliesTheSetup()
        {
            PlayTurn(state => state.Fighters[Side.Left].Hp = 55f);
            EnterByDoubleKo();

            Assert.That(_machine.Phase, Is.EqualTo(MatchPhase.SuddenDeathSetup));
            Assert.That(_machine.State.Fighters[Side.Left].Hp, Is.EqualTo(RuleConstants.SuddenDeathHp));
            Assert.That(_machine.State.Fighters[Side.Left].HpBeforeSuddenDeath, Is.EqualTo(0f), "a double KO: both had nothing left");
            Assert.That(_machine.State.SuddenDeathRepeats, Is.EqualTo(0));
        }

        [Test]
        public void TheOptions_StayInSuddenDeath_ByDefault()
        {
            EnterByDoubleKo();

            Assert.That(_machine.Constraints[Side.Left].CanSwitchWeapon, Is.True);
            Assert.That(_machine.Constraints[Side.Right].CanUseSignature, Is.True);
        }

        [Test]
        public void TheOptionFlags_BecomePlanningLimits_OnlyInSuddenDeath()
        {
            _settings.SuddenDeath.AllowWeaponSwitching = false;
            _settings.SuddenDeath.AllowSignatureMoves = false;
            PlayTurn();
            Assert.That(_machine.Constraints[Side.Left].CanSwitchWeapon, Is.True, "a normal turn");

            EnterByDoubleKo();

            foreach (Side side in new[] { Side.Left, Side.Right })
            {
                Assert.That(_machine.Constraints[side].CanSwitchWeapon, Is.False, side.ToString());
                Assert.That(_machine.Constraints[side].CanUseSignature, Is.False, side.ToString());
            }

            _machine.BeginPlanning(Tips);
            Assert.That(_machine.Apply(Side.Left, new SelectWeaponCommand(WeaponIds.Mace)).Accepted, Is.False, "switching refused");
        }

        [Test]
        public void ATiedSuddenDeathTurn_IsPlayedAgain_BothBackAtOneHp_AndCounted()
        {
            EnterByDoubleKo();
            SimTime instant = SimTime.FromMilliseconds(80);

            EndCheckResult result = PlayTurn(state =>
            {
                state.Fighters[Side.Left].Hp = 0f;
                state.Fighters[Side.Right].Hp = 0f;
            }, new PerSide<SimTime?>(instant, instant), new PerSide<SimTime?>(instant, instant));

            Assert.That(result.Resolution, Is.EqualTo(TurnResolution.RepeatSuddenDeathTurn));
            Assert.That(_machine.Phase, Is.EqualTo(MatchPhase.TurnStart));
            Assert.That(_machine.State.IsSuddenDeath, Is.True);
            Assert.That(_machine.State.Fighters[Side.Left].Hp, Is.EqualTo(RuleConstants.SuddenDeathHp));
            Assert.That(_machine.State.Fighters[Side.Right].Hp, Is.EqualTo(RuleConstants.SuddenDeathHp));
            Assert.That(_machine.State.SuddenDeathRepeats, Is.EqualTo(1));
        }

        [Test]
        public void TheDeathTimes_ReachTheEvaluator()
        {
            EnterByDoubleKo();

            EndCheckResult result = PlayTurn(state => state.Fighters[Side.Right].Hp = 0f,
                deaths: new PerSide<SimTime?>(null, SimTime.FromMilliseconds(90)));

            Assert.That(result.Outcome.Winner, Is.EqualTo(Side.Left));
            Assert.That(result.Outcome.Reason, Is.EqualTo(MatchEndReason.KnockOut), "a death without a hit (its own wall)");
        }

        [Test]
        public void SuddenDeath_AlwaysEnds_AfterOnePlusTheCapTurnsWithoutAHit()
        {
            EnterByDoubleKo();
            int suddenDeathTurns = 0;
            EndCheckResult result;
            do
            {
                result = PlayTurn();
                suddenDeathTurns++;
            }
            while (result.Resolution == TurnResolution.RepeatSuddenDeathTurn && suddenDeathTurns < 100);

            Assert.That(result.Resolution, Is.EqualTo(TurnResolution.MatchOver));
            Assert.That(suddenDeathTurns, Is.EqualTo(1 + _settings.SuddenDeath.MaxRepeatsWithoutHit));
            Assert.That(result.Outcome.Reason, Is.EqualTo(MatchEndReason.SuddenDeathCoinToss), "a double KO leaves both level at 0 HP");
            Assert.That(_machine.Phase, Is.EqualTo(MatchPhase.MatchOver));
        }
    }
}
