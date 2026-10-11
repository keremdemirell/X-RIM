using System;
using NUnit.Framework;
using XRim.Core;
using XRim.Rules;
using XRim.Rules.Match;
using XRim.Rules.Settings;
using XRim.Rules.SuddenDeath;

namespace XRim.Tests.EditMode.Rules.Match
{
    /// <summary>
    /// KO, double KO, forfeit and the turn cap, in the D11 order (GDD §3, §13), and the sudden-death turn (§14): the first decisive moment
    /// (P4), the tie policy, forfeits and the capped no-hit policy with its fallback (P6; designer 2026-10-11).
    /// </summary>
    public sealed class EndConditionEvaluatorTests
    {
        private const int LastTurnIndex = 29; // the 30th turn, with the default cap of 30

        private readonly EndConditionEvaluator _evaluator = new EndConditionEvaluator();
        private RulesSettings _settings;
        private RulePolicies _policies;

        [SetUp]
        public void SetUp()
        {
            _settings = GddStartingValues.CreateRulesSettings();
            _policies = new RulePolicies();
        }

        private ExecutionReport Report(Action<MatchState> tweak = null, PerSide<SimTime?> hits = null, PerSide<SimTime?> deaths = null,
            bool sameStep = false)
        {
            MatchState state = MatchState.CreateInitial(TestData.CreateMatchSetup(), _settings);
            tweak?.Invoke(state);
            return new ExecutionReport(state, hits ?? new PerSide<SimTime?>(null, null), sameStep, deaths);
        }

        private EndCheckResult Evaluate(Action<MatchState> tweak = null) => Evaluate(Report(tweak));

        private EndCheckResult Evaluate(ExecutionReport report, uint seed = TestData.Seed) => _evaluator.Evaluate(report, _settings, _policies, seed);

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

        // --- Sudden death: the first decisive moment wins (§14 Decided, P4) ---------------------------

        private static SimTime Ms(double milliseconds) => SimTime.FromMilliseconds(milliseconds);

        private static PerSide<SimTime?> Times(SimTime? left, SimTime? right) => new PerSide<SimTime?>(left, right);

        private static void SuddenDeath(MatchState state)
        {
            state.IsSuddenDeath = true;
            state.TurnIndex = LastTurnIndex + 1;
            state.Fighters[Side.Left].Hp = RuleConstants.SuddenDeathHp;
            state.Fighters[Side.Right].Hp = RuleConstants.SuddenDeathHp;
        }

        [TestCase(Side.Left)]
        [TestCase(Side.Right)]
        public void InSuddenDeath_TheFirstValidHit_WinsTheMatch(Side hitter)
        {
            Side victim = hitter.Opponent();
            var hits = new PerSide<SimTime?>(null, null);
            var deaths = new PerSide<SimTime?>(null, null);
            hits[hitter] = Ms(40);
            deaths[victim] = Ms(40); // at 1 HP the hit kills

            EndCheckResult result = Evaluate(Report(state =>
            {
                SuddenDeath(state);
                Kill(state, victim);
            }, hits, deaths));

            AssertMatchOver(result, hitter, MatchEndReason.SuddenDeathHit, LastTurnIndex + 2);
        }

        [Test]
        public void InSuddenDeath_AValidHitTooWeakToKill_StillWins()
        {
            EndCheckResult result = Evaluate(Report(SuddenDeath, hits: Times(null, Ms(120))));

            AssertMatchOver(result, Side.Right, MatchEndReason.SuddenDeathHit, LastTurnIndex + 2);
        }

        [Test]
        public void InSuddenDeath_BothHit_TheEarlierTimeToImpactWins_ToTheMicrosecond()
        {
            var earlier = new SimTime(50_000L);
            var later = new SimTime(50_001L);

            Assert.That(Evaluate(Report(SuddenDeath, hits: Times(earlier, later))).Outcome.Winner, Is.EqualTo(Side.Left));
            Assert.That(Evaluate(Report(SuddenDeath, hits: Times(later, earlier))).Outcome.Winner, Is.EqualTo(Side.Right));
        }

        [Test]
        public void InSuddenDeath_TwoHitsInTheSameStep_AreStillOrderedByTheirTimes()
        {
            EndCheckResult result = Evaluate(Report(SuddenDeath, hits: Times(Ms(50.2), Ms(50.1)), sameStep: true));

            AssertMatchOver(result, Side.Right, MatchEndReason.SuddenDeathHit, LastTurnIndex + 2);
        }

        [Test]
        public void InSuddenDeath_ExactlyTheSameInstant_GoesToTheTiePolicy_WhichReplaysByDefault()
        {
            Assert.That(_policies.SuddenDeathTie, Is.TypeOf<ReplayTiePolicy>(), "the designer's default (2026-10-11)");

            EndCheckResult result = Evaluate(Report(state =>
            {
                SuddenDeath(state);
                Kill(state, Side.Left);
                Kill(state, Side.Right);
            }, hits: Times(Ms(75), Ms(75)), deaths: Times(Ms(75), Ms(75))));

            Assert.That(result.Resolution, Is.EqualTo(TurnResolution.RepeatSuddenDeathTurn));
        }

        [TestCase(SuddenDeathTieOutcome.LeftWins, Side.Left)]
        [TestCase(SuddenDeathTieOutcome.RightWins, Side.Right)]
        public void TheTiePolicy_CanNameAWinner(SuddenDeathTieOutcome outcome, Side winner)
        {
            _policies.SuddenDeathTie = new FixedTiePolicy(outcome);

            EndCheckResult result = Evaluate(Report(SuddenDeath, hits: Times(Ms(75), Ms(75))));

            AssertMatchOver(result, winner, MatchEndReason.SuddenDeathHit, LastTurnIndex + 2);
        }

        [Test]
        public void InSuddenDeath_ADeathWithoutAHit_LosesAtThatMoment()
        {
            // Left backs into its live wall at 100 ms (walls switched on in sudden death); right never hits.
            EndCheckResult zapped = Evaluate(Report(state =>
            {
                SuddenDeath(state);
                Kill(state, Side.Left);
            }, deaths: Times(Ms(100), null)));
            AssertMatchOver(zapped, Side.Right, MatchEndReason.KnockOut, LastTurnIndex + 2);

            // Right's hit on the falling dummy at 300 ms changes nothing: the zap came first.
            Assert.That(Evaluate(Report(SuddenDeath, hits: Times(null, Ms(300)), deaths: Times(Ms(100), null))).Outcome.Reason,
                Is.EqualTo(MatchEndReason.KnockOut));

            // Left hit first, then fell to its own wall: the first decisive moment was left's hit.
            EndCheckResult hitFirst = Evaluate(Report(SuddenDeath, hits: Times(Ms(50), null), deaths: Times(Ms(100), Ms(50))));
            AssertMatchOver(hitFirst, Side.Left, MatchEndReason.SuddenDeathHit, LastTurnIndex + 2);
        }

        [Test]
        public void InSuddenDeath_ADeathTheReportDidNotTime_CountsAsTheTurnsEnd()
        {
            Assert.That(Evaluate(Report(state =>
            {
                SuddenDeath(state);
                Kill(state, Side.Right);
            })).Outcome.Winner, Is.EqualTo(Side.Left));

            EndCheckResult result = Evaluate(Report(state =>
            {
                SuddenDeath(state);
                Kill(state, Side.Left);
            }, hits: Times(Ms(200), null)));
            AssertMatchOver(result, Side.Left, MatchEndReason.SuddenDeathHit, LastTurnIndex + 2);
        }

        [Test]
        public void InSuddenDeath_TheTurnCapDoesNotFireAgain()
        {
            EndCheckResult result = Evaluate(state =>
            {
                state.IsSuddenDeath = true;
                state.TurnIndex = LastTurnIndex + 5;
            });

            Assert.That(result.Resolution, Is.EqualTo(TurnResolution.RepeatSuddenDeathTurn), "no hit: the turn repeats");
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
        public void InSuddenDeath_AHitBeatsAForfeit()
        {
            EndCheckResult result = Evaluate(Report(state =>
            {
                SuddenDeath(state);
                Idle(state, Side.Left, RuleConstants.IdleTurnsBeforeForfeit);
            }, hits: Times(Ms(30), null)));

            AssertMatchOver(result, Side.Left, MatchEndReason.SuddenDeathHit, LastTurnIndex + 2);
        }

        // --- Sudden death: nobody wins the turn (§14 TBD, P6) ----------------------------------------

        [Test]
        public void NobodyHits_TheTurnRepeats_UntilTheCapRunsOut_ThenTheFallbackDecides()
        {
            Assert.That(_settings.SuddenDeath.MaxRepeatsWithoutHit, Is.EqualTo(3), "the designer's default (2026-10-11)");

            for (int repeats = 0; repeats < 3; repeats++)
            {
                int done = repeats;
                Assert.That(Evaluate(state =>
                {
                    SuddenDeath(state);
                    state.SuddenDeathRepeats = done;
                }).Resolution, Is.EqualTo(TurnResolution.RepeatSuddenDeathTurn), $"after {done} repeats");
            }

            EndCheckResult last = Evaluate(state =>
            {
                SuddenDeath(state);
                state.SuddenDeathRepeats = 3;
            });
            Assert.That(last.Resolution, Is.EqualTo(TurnResolution.MatchOver), "1 + 3 sudden-death turns at most");
        }

        [Test]
        public void TheFallback_MoreHpWhenSuddenDeathBegan_Wins()
        {
            EndCheckResult result = Evaluate(state =>
            {
                SuddenDeath(state);
                state.SuddenDeathRepeats = 3;
                state.Fighters[Side.Left].HpBeforeSuddenDeath = 40f;
                state.Fighters[Side.Right].HpBeforeSuddenDeath = 70f;
            });

            AssertMatchOver(result, Side.Right, MatchEndReason.SuddenDeathMoreHp, LastTurnIndex + 2);
        }

        [Test]
        public void TheFallback_LevelOnHp_IsACoinTossFromTheMatchSeed_TheSameForTheSameMatch()
        {
            ExecutionReport level = Report(state =>
            {
                SuddenDeath(state);
                state.SuddenDeathRepeats = 3;
            });

            EndCheckResult first = Evaluate(level, 7u);
            Assert.That(first.Outcome.Reason, Is.EqualTo(MatchEndReason.SuddenDeathCoinToss));
            Assert.That(Evaluate(level, 7u).Outcome.Winner, Is.EqualTo(first.Outcome.Winner), "the same seed, the same toss");

            var winners = new System.Collections.Generic.HashSet<Side>();
            for (uint seed = 1u; seed <= 32u; seed++) winners.Add(Evaluate(level, seed).Outcome.Winner);
            Assert.That(winners, Is.EquivalentTo(new[] { Side.Left, Side.Right }), "either side can win the toss");
        }

        [Test]
        public void TheCap_IsTunable_ZeroGoesStraightToTheFallback()
        {
            _settings.SuddenDeath.MaxRepeatsWithoutHit = 0;

            Assert.That(Evaluate(SuddenDeath).Resolution, Is.EqualTo(TurnResolution.MatchOver));
        }

        [Test]
        public void ATieReplay_CountsTowardTheCap()
        {
            EndCheckResult result = Evaluate(Report(state =>
            {
                SuddenDeath(state);
                state.SuddenDeathRepeats = 3;
            }, hits: Times(Ms(75), Ms(75))));

            Assert.That(result.Resolution, Is.EqualTo(TurnResolution.MatchOver));
            Assert.That(result.Outcome.Reason, Is.EqualTo(MatchEndReason.SuddenDeathCoinToss));
        }

        [Test]
        public void InSuddenDeath_BothForfeiting_RepeatsTheTurn_WithinTheCap()
        {
            Assert.That(Evaluate(state =>
            {
                state.IsSuddenDeath = true;
                Idle(state, Side.Left, RuleConstants.IdleTurnsBeforeForfeit);
                Idle(state, Side.Right, RuleConstants.IdleTurnsBeforeForfeit);
            }).Resolution, Is.EqualTo(TurnResolution.RepeatSuddenDeathTurn));

            Assert.That(Evaluate(state =>
            {
                state.IsSuddenDeath = true;
                state.SuddenDeathRepeats = 3;
                Idle(state, Side.Left, RuleConstants.IdleTurnsBeforeForfeit);
                Idle(state, Side.Right, RuleConstants.IdleTurnsBeforeForfeit);
            }).Resolution, Is.EqualTo(TurnResolution.MatchOver));
        }

        [Test]
        public void TheNoHitPolicy_IsASeam()
        {
            _policies.SuddenDeathNoHit = new AlwaysLeftPolicy();

            AssertMatchOver(Evaluate(SuddenDeath), Side.Left, MatchEndReason.SuddenDeathMoreHp, LastTurnIndex + 2);
        }

        private sealed class FixedTiePolicy : ITiePolicy
        {
            private readonly SuddenDeathTieOutcome _outcome;

            public FixedTiePolicy(SuddenDeathTieOutcome outcome)
            {
                _outcome = outcome;
            }

            public SuddenDeathTieOutcome Resolve(SimTime leftImpact, SimTime rightImpact) => _outcome;
        }

        private sealed class AlwaysLeftPolicy : ISuddenDeathNoHitPolicy
        {
            public SuddenDeathNoHitDecision Decide(MatchState state, SuddenDeathSettings settings, uint matchSeed) =>
                SuddenDeathNoHitDecision.Win(Side.Left, MatchEndReason.SuddenDeathMoreHp);
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

            _evaluator.Evaluate(report, _settings, _policies, TestData.Seed);

            MatchState state2 = report.ResolvedState;
            Assert.That(state2.Fighters[Side.Left].Hp, Is.EqualTo(0f));
            Assert.That(state2.Fighters[Side.Right].ConsecutiveIdleTurns, Is.EqualTo(2));
            Assert.That(state2.TurnIndex, Is.EqualTo(8));
            Assert.That(state2.IsSuddenDeath, Is.False);
        }
    }
}
