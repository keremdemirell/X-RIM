using XRim.Core;
using XRim.Core.Gdd;
using XRim.Rules.Settings;
using XRim.Rules.SuddenDeath;

namespace XRim.Rules.Match
{
    /// <summary>
    /// The single place end conditions are checked after each turn. In a normal turn they are checked in this order
    /// (D11, not in the GDD and not decided by the designer; this is the recommended order, built as a flagged seam):
    /// <list type="number">
    /// <item>Double KO: sudden death (§3, Decided).</item>
    /// <item>Single KO: the other dummy wins (§3, Decided).</item>
    /// <item>Forfeit: a side with <see cref="RuleConstants.IdleTurnsBeforeForfeit"/> idle turns in a row loses
    /// (§3, Decided). If both reach it in the same turn, <see cref="MatchSettings.BothForfeitRule"/> decides.</item>
    /// <item>Turn cap: sudden death after turn <see cref="MatchSettings.TurnCap"/> has resolved (§3, §13).</item>
    /// </list>
    /// So a KO always beats the clock. The idle counters in the resolved state must already include this turn:
    /// <see cref="MatchStateMachine"/> updates them (through <c>IIdleTurnPolicy</c>) before it calls the evaluator.
    /// The evaluator only reads the state.
    /// <para>
    /// A sudden-death turn (§14; the turn cap never fires again inside it):
    /// <list type="number">
    /// <item>The first decisive moment wins (Decided: the first valid hit wins, the earlier time-to-impact when both hit; P4, designer
    /// 2026-10-11): a side wins at its first valid hit, even one too weak to bring HP to 0, or when the other side dies without being hit
    /// (its own electric wall). Times are compared in microseconds; only exactly the same instant goes to <see cref="ITiePolicy"/>.</item>
    /// <item>Idle players still forfeit (§3, Decided).</item>
    /// <item>No winner (nobody hit, a tie replayed, both idle): <see cref="ISuddenDeathNoHitPolicy"/> plays the turn again or, once its
    /// repeats run out, names the winner (P6), so sudden death always ends.</item>
    /// </list>
    /// </para>
    /// </summary>
    [GddTbd("§3", "Order of end conditions when they happen in the same turn (not covered by the GDD)",
        Proposal = "Double KO, then single KO, then forfeit, then turn cap")]
    public sealed class EndConditionEvaluator
    {
        /// <summary>A death the report has no time for (a source that does not record deaths) counts as the turn's last instant.</summary>
        private static readonly SimTime UntimedDeath = new SimTime(long.MaxValue);

        /// <param name="matchSeed">The match's seed (<see cref="MatchSetup.RandomSeed"/>), for a policy's random choice.</param>
        public EndCheckResult Evaluate(ExecutionReport report, RulesSettings settings, RulePolicies policies, uint matchSeed)
        {
            Guard.NotNull(report, nameof(report));
            Guard.NotNull(settings, nameof(settings));
            Guard.NotNull(policies, nameof(policies));

            MatchState state = report.ResolvedState;
            return state.IsSuddenDeath ? EvaluateSuddenDeathTurn(report, settings, policies, matchSeed) : EvaluateNormalTurn(state, settings);
        }

        private static EndCheckResult EvaluateNormalTurn(MatchState state, RulesSettings settings)
        {
            bool leftDead = state.Fighters[Side.Left].IsDead;
            bool rightDead = state.Fighters[Side.Right].IsDead;

            if (leftDead && rightDead) return Resolution(TurnResolution.EnterSuddenDeath);
            if (leftDead || rightDead) return MatchOver(state, leftDead ? Side.Right : Side.Left, MatchEndReason.KnockOut);
            if (TryForfeit(state, settings, TurnResolution.EnterSuddenDeath, out EndCheckResult forfeit)) return forfeit;

            // The turn cap counts turns played: turn index 29 is the 30th turn.
            if (state.TurnIndex + 1 >= settings.Match.TurnCap) return Resolution(TurnResolution.EnterSuddenDeath);

            return Resolution(TurnResolution.NextTurn);
        }

        private static EndCheckResult EvaluateSuddenDeathTurn(ExecutionReport report, RulesSettings settings, RulePolicies policies, uint matchSeed)
        {
            MatchState state = report.ResolvedState;
            SimTime? leftWins = WinningMoment(report, Side.Left);
            SimTime? rightWins = WinningMoment(report, Side.Right);
            if (leftWins.HasValue || rightWins.HasValue)
            {
                if (!rightWins.HasValue || (leftWins.HasValue && leftWins.Value < rightWins.Value))
                    return MatchOver(state, Side.Left, SuddenDeathReason(report, Side.Left, leftWins.Value));
                if (!leftWins.HasValue || rightWins.Value < leftWins.Value)
                    return MatchOver(state, Side.Right, SuddenDeathReason(report, Side.Right, rightWins.Value));

                // Exactly the same instant: the tie policy, whose replay is a turn without a winner like any other (P6).
                ITiePolicy tie = Guard.NotNull(policies.SuddenDeathTie, nameof(RulePolicies.SuddenDeathTie));
                switch (tie.Resolve(leftWins.Value, rightWins.Value))
                {
                    case SuddenDeathTieOutcome.LeftWins: return MatchOver(state, Side.Left, MatchEndReason.SuddenDeathHit);
                    case SuddenDeathTieOutcome.RightWins: return MatchOver(state, Side.Right, MatchEndReason.SuddenDeathHit);
                }
            }
            else if (TryForfeit(state, settings, TurnResolution.RepeatSuddenDeathTurn, out EndCheckResult forfeit) &&
                     forfeit.Resolution == TurnResolution.MatchOver)
            {
                return forfeit;
            }

            // No winner this turn: nobody hit, a tie replayed, or both idled out.
            ISuddenDeathNoHitPolicy noHit = Guard.NotNull(policies.SuddenDeathNoHit, nameof(RulePolicies.SuddenDeathNoHit));
            SuddenDeathNoHitDecision decision = noHit.Decide(state, settings.SuddenDeath, matchSeed);
            return decision.RepeatsTurn ? Resolution(TurnResolution.RepeatSuddenDeathTurn) : MatchOver(state, decision.Winner, decision.Reason);
        }

        /// <summary>
        /// The moment a side won this sudden-death turn, if it did: its first valid hit, or the other side's death without a hit, whichever
        /// came first (a death the report did not time counts as the turn's end).
        /// </summary>
        private static SimTime? WinningMoment(ExecutionReport report, Side side)
        {
            SimTime? hit = report.FirstValidHitTime[side];
            SimTime? opponentDied = report.DeathTime[side.Opponent()];
            if (!opponentDied.HasValue && report.ResolvedState.Fighters[side.Opponent()].IsDead) opponentDied = UntimedDeath;
            if (!hit.HasValue) return opponentDied;
            if (!opponentDied.HasValue) return hit;
            return hit.Value <= opponentDied.Value ? hit : opponentDied;
        }

        /// <summary>Won by a hit, or by the other dummy dying without one (a knockout, such as its own wall).</summary>
        private static MatchEndReason SuddenDeathReason(ExecutionReport report, Side winner, SimTime moment) =>
            report.FirstValidHitTime[winner] == moment ? MatchEndReason.SuddenDeathHit : MatchEndReason.KnockOut;

        /// <param name="whenNoWinner">What a forfeit by both sides with no winner resolves to in this kind of turn.</param>
        private static bool TryForfeit(MatchState state, RulesSettings settings, TurnResolution whenNoWinner, out EndCheckResult result)
        {
            bool leftForfeits = HasForfeited(state, Side.Left);
            bool rightForfeits = HasForfeited(state, Side.Right);

            if (leftForfeits != rightForfeits)
            {
                result = MatchOver(state, leftForfeits ? Side.Right : Side.Left, MatchEndReason.Forfeit);
                return true;
            }

            if (!leftForfeits)
            {
                result = default;
                return false;
            }

            float leftHp = state.Fighters[Side.Left].Hp;
            float rightHp = state.Fighters[Side.Right].Hp;
            if (settings.Match.BothForfeitRule == BothForfeitRule.HigherHpWins && leftHp != rightHp)
            {
                result = MatchOver(state, leftHp > rightHp ? Side.Left : Side.Right, MatchEndReason.Forfeit);
                return true;
            }

            result = Resolution(whenNoWinner);
            return true;
        }

        private static bool HasForfeited(MatchState state, Side side) =>
            state.Fighters[side].ConsecutiveIdleTurns >= RuleConstants.IdleTurnsBeforeForfeit;

        private static EndCheckResult Resolution(TurnResolution resolution) => new EndCheckResult(resolution, null);

        private static EndCheckResult MatchOver(MatchState state, Side winner, MatchEndReason reason) =>
            new EndCheckResult(TurnResolution.MatchOver, new MatchOutcome(winner, reason, state.TurnIndex + 1));
    }
}
