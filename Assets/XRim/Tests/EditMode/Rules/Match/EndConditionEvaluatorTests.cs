using System;
using NUnit.Framework;
using XRim.Core;
using XRim.Rules;
using XRim.Rules.Match;
using XRim.Rules.Settings;

namespace XRim.Tests.EditMode.Rules.Match
{
    /// <summary>KO, double KO, forfeit and the turn cap, in the D11 order (GDD §3, §13, §14).</summary>
    public sealed class EndConditionEvaluatorTests
    {
        private const int LastTurnIndex = 29; // the 30th turn, with the default cap of 30

        private readonly EndConditionEvaluator _evaluator = new EndConditionEvaluator();
        private RulesSettings _settings;

        [SetUp]
        public void SetUp()
        {
            _settings = GddStartingValues.CreateRulesSettings();
        }

        private ExecutionReport Report(Action<MatchState> tweak = null)
        {
            MatchState state = MatchState.CreateInitial(TestData.CreateMatchSetup(), _settings);
            tweak?.Invoke(state);
            return new ExecutionReport(state, new PerSide<SimTime?>(null, null), false);
        }

        private EndCheckResult Evaluate(Action<MatchState> tweak = null) => _evaluator.Evaluate(Report(tweak), _settings);

        private static void Kill(MatchState state, Side side) => state.Fighters[side].Hp = 0f;

        private static void Idle(MatchState state, Side side, int turns) => state.Fighters[side].ConsecutiveIdleTurns = turns;

        private static void AssertMatchOver(EndCheckResult result, Side winner, MatchEndReason reason, int turnCount)
        {
            Assert.That(result.Resolution, Is.EqualTo(TurnResolution.MatchOver));
            Assert.That(result.Outcome, Is.Not.Null);
            Assert.That(result.Outcome.Winner, Is.EqualTo(winner));
            Assert.That(result.Outcome.Reason, Is.EqualTo(reason));
            Assert.That(result.Outcome.TurnCount, Is.EqualTo(turnCount));
        }

        // --- Nothing special -------------------------------------------------------------------

        [Test]
        public void NothingHappened_PlaysTheNextTurn()
        {
            EndCheckResult result = Evaluate();

            Assert.That(result.Resolution, Is.EqualTo(TurnResolution.NextTurn));
            Assert.That(result.Outcome, Is.Null);
        }

        // --- KO and double KO (§3) -------------------------------------------------------------

        [TestCase(Side.Left, Side.Right)]
        [TestCase(Side.Right, Side.Left)]
        public void ASingleKo_EndsTheMatch_ForTheOtherDummy(Side dead, Side winner)
        {
            EndCheckResult result = Evaluate(state =>
            {
                state.TurnIndex = 6;
                Kill(state, dead);
            });

            AssertMatchOver(result, winner, MatchEndReason.KnockOut, 7);
        }

        [Test]
        public void ADoubleKo_GoesToSuddenDeath()
        {
            EndCheckResult result = Evaluate(state =>
            {
                Kill(state, Side.Left);
                Kill(state, Side.Right);
            });

            Assert.That(result.Resolution, Is.EqualTo(TurnResolution.EnterSuddenDeath));
            Assert.That(result.Outcome, Is.Null);
        }

        // --- Forfeit (§3, D10) -----------------------------------------------------------------

        [TestCase(Side.Left, Side.Right)]
        [TestCase(Side.Right, Side.Left)]
        public void ThreeIdleTurnsInARow_LoseByForfeit(Side idle, Side winner)
        {
            EndCheckResult result = Evaluate(state =>
            {
                state.TurnIndex = 4;
                Idle(state, idle, RuleConstants.IdleTurnsBeforeForfeit);
            });

            AssertMatchOver(result, winner, MatchEndReason.Forfeit, 5);
        }

        [Test]
        public void TwoIdleTurns_AreNotYetAForfeit()
        {
            EndCheckResult result = Evaluate(state => Idle(state, Side.Left, RuleConstants.IdleTurnsBeforeForfeit - 1));

            Assert.That(result.Resolution, Is.EqualTo(TurnResolution.NextTurn));
        }

        [Test]
        public void TheForfeitLimit_IsThree()
        {
            Assert.That(RuleConstants.IdleTurnsBeforeForfeit, Is.EqualTo(3), "GDD §3 (Decided)");
        }

        [Test]
        public void BothForfeiting_GoesToSuddenDeathByDefault()
        {
            EndCheckResult result = Evaluate(state =>
            {
                Idle(state, Side.Left, RuleConstants.IdleTurnsBeforeForfeit);
                Idle(state, Side.Right, RuleConstants.IdleTurnsBeforeForfeit + 2);
            });

            Assert.That(_settings.Match.BothForfeitRule, Is.EqualTo(BothForfeitRule.SuddenDeath), "designer's pick, 2026-10-01");
            Assert.That(result.Resolution, Is.EqualTo(TurnResolution.EnterSuddenDeath));
        }

        [Test]
        public void BothForfeiting_CanInsteadGoToTheHigherHp()
        {
            _settings.Match.BothForfeitRule = BothForfeitRule.HigherHpWins;

            EndCheckResult result = Evaluate(state =>
            {
                Idle(state, Side.Left, RuleConstants.IdleTurnsBeforeForfeit);
                Idle(state, Side.Right, RuleConstants.IdleTurnsBeforeForfeit);
                state.Fighters[Side.Right].Hp = 40f;
            });

            AssertMatchOver(result, Side.Left, MatchEndReason.Forfeit, 1);
        }

        [Test]
        public void BothForfeiting_WithEqualHp_StillGoesToSuddenDeath_WhenHigherHpWins()
        {
            _settings.Match.BothForfeitRule = BothForfeitRule.HigherHpWins;

            EndCheckResult result = Evaluate(state =>
            {
                Idle(state, Side.Left, RuleConstants.IdleTurnsBeforeForfeit);
                Idle(state, Side.Right, RuleConstants.IdleTurnsBeforeForfeit);
            });

            Assert.That(result.Resolution, Is.EqualTo(TurnResolution.EnterSuddenDeath));
        }

        // --- Turn cap (§3, §13) ----------------------------------------------------------------

        [Test]
        public void TheTurnCap_StartsSuddenDeath_AfterTurn30()
        {
            Assert.That(Evaluate(state => state.TurnIndex = LastTurnIndex - 1).Resolution, Is.EqualTo(TurnResolution.NextTurn));
            Assert.That(Evaluate(state => state.TurnIndex = LastTurnIndex).Resolution, Is.EqualTo(TurnResolution.EnterSuddenDeath));
        }

        [Test]
        public void TheTurnCap_IsTunable()
        {
            _settings.Match.TurnCap = 5;

            Assert.That(Evaluate(state => state.TurnIndex = 3).Resolution, Is.EqualTo(TurnResolution.NextTurn));
            Assert.That(Evaluate(state => state.TurnIndex = 4).Resolution, Is.EqualTo(TurnResolution.EnterSuddenDeath));
        }

        // --- The order of end conditions (D11) -------------------------------------------------

        [Test]
        public void Order_AKoBeatsAForfeitInTheSameTurn()
        {
            // Left is knocked out while right is on its third idle turn: the KO decides, so right wins.
            EndCheckResult result = Evaluate(state =>
            {
                Kill(state, Side.Left);
                Idle(state, Side.Right, RuleConstants.IdleTurnsBeforeForfeit);
            });

            AssertMatchOver(result, Side.Right, MatchEndReason.KnockOut, 1);
        }

        [Test]
        public void Order_ADoubleKoBeatsAForfeit()
        {
            EndCheckResult result = Evaluate(state =>
            {
                Kill(state, Side.Left);
                Kill(state, Side.Right);
                Idle(state, Side.Left, RuleConstants.IdleTurnsBeforeForfeit);
            });

            Assert.That(result.Resolution, Is.EqualTo(TurnResolution.EnterSuddenDeath));
        }

        [Test]
        public void Order_AKoBeatsTheTurnCap()
        {
            EndCheckResult result = Evaluate(state =>
            {
                state.TurnIndex = LastTurnIndex;
                Kill(state, Side.Right);
            });

            AssertMatchOver(result, Side.Left, MatchEndReason.KnockOut, 30);
        }

        [Test]
        public void Order_AForfeitBeatsTheTurnCap()
        {
            EndCheckResult result = Evaluate(state =>
            {
                state.TurnIndex = LastTurnIndex;
                Idle(state, Side.Left, RuleConstants.IdleTurnsBeforeForfeit);
            });

            AssertMatchOver(result, Side.Right, MatchEndReason.Forfeit, 30);
        }

        // --- Sudden death: the transition's edges (the rest is Session 12) ---------------------

        [Test]
        public void InSuddenDeath_ASingleKoIsTheFirstHit_AndWinsTheMatch()
        {
            EndCheckResult result = Evaluate(state =>
            {
                state.IsSuddenDeath = true;
                state.TurnIndex = 31;
                Kill(state, Side.Right);
            });

            AssertMatchOver(result, Side.Left, MatchEndReason.SuddenDeathHit, 32);
        }

        [Test]
        public void InSuddenDeath_TheTurnCapDoesNotFireAgain()
        {
            EndCheckResult result = Evaluate(state =>
            {
                state.IsSuddenDeath = true;
                state.TurnIndex = LastTurnIndex + 5;
            });

            Assert.That(result.Resolution, Is.EqualTo(TurnResolution.RepeatSuddenDeathTurn), "no hit: the turn repeats (Session 12 seam)");
        }

        [Test]
        public void InSuddenDeath_BothHitting_IsLeftToSession12()
        {
            EndCheckResult result = Evaluate(state =>
            {
                state.IsSuddenDeath = true;
                Kill(state, Side.Left);
                Kill(state, Side.Right);
            });

            Assert.That(result.Resolution, Is.EqualTo(TurnResolution.RepeatSuddenDeathTurn), "Session 12 seam: earlier hit or tie policy");
        }

        [Test]
        public void InSuddenDeath_AnIdlePlayerStillForfeits()
        {
            EndCheckResult result = Evaluate(state =>
            {
                state.IsSuddenDeath = true;
                Idle(state, Side.Right, RuleConstants.IdleTurnsBeforeForfeit);
            });

            AssertMatchOver(result, Side.Left, MatchEndReason.Forfeit, 1);
        }

        [Test]
        public void InSuddenDeath_BothForfeiting_RepeatsTheTurn()
        {
            EndCheckResult result = Evaluate(state =>
            {
                state.IsSuddenDeath = true;
                Idle(state, Side.Left, RuleConstants.IdleTurnsBeforeForfeit);
                Idle(state, Side.Right, RuleConstants.IdleTurnsBeforeForfeit);
            });

            Assert.That(result.Resolution, Is.EqualTo(TurnResolution.RepeatSuddenDeathTurn));
        }

        // --- The evaluator only reads ----------------------------------------------------------

        [Test]
        public void Evaluating_ChangesNothing()
        {
            ExecutionReport report = Report(state =>
            {
                Kill(state, Side.Left);
                Idle(state, Side.Right, 2);
                state.TurnIndex = 8;
            });

            _evaluator.Evaluate(report, _settings);

            MatchState state2 = report.ResolvedState;
            Assert.That(state2.Fighters[Side.Left].Hp, Is.EqualTo(0f));
            Assert.That(state2.Fighters[Side.Right].ConsecutiveIdleTurns, Is.EqualTo(2));
            Assert.That(state2.TurnIndex, Is.EqualTo(8));
            Assert.That(state2.IsSuddenDeath, Is.False);
        }
    }
}
