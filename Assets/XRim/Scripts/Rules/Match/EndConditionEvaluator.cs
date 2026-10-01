using XRim.Core;
using XRim.Core.Gdd;
using XRim.Rules.Settings;

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
    /// Sudden death itself is Session 12: the turn cap never fires again inside it, a single KO ends the match with
    /// <see cref="MatchEndReason.SuddenDeathHit"/>, and every other sudden-death result is one marked seam.
    /// </para>
    /// </summary>
    [GddTbd("§3", "Order of end conditions when they happen in the same turn (not covered by the GDD)",
        Proposal = "Double KO, then single KO, then forfeit, then turn cap")]
    public sealed class EndConditionEvaluator
    {
        public EndCheckResult Evaluate(ExecutionReport report, RulesSettings settings)
        {
            Guard.NotNull(report, nameof(report));
            Guard.NotNull(settings, nameof(settings));

            MatchState state = report.ResolvedState;
            return state.IsSuddenDeath ? EvaluateSuddenDeathTurn(state, settings) : EvaluateNormalTurn(state, settings);
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

        private static EndCheckResult EvaluateSuddenDeathTurn(MatchState state, RulesSettings settings)
        {
            bool leftDead = state.Fighters[Side.Left].IsDead;
            bool rightDead = state.Fighters[Side.Right].IsDead;

            // With 1 HP the first valid hit wins (§14, Decided).
            if (leftDead != rightDead) return MatchOver(state, leftDead ? Side.Right : Side.Left, MatchEndReason.SuddenDeathHit);

            // SESSION 12 SEAM (§14, TBD): both dummies hit (the earlier time-to-impact wins, an exact same-step tie goes
            // to ITiePolicy) or nobody hits (ISuddenDeathNoHitPolicy, which may cap the repeats). Until then the
            // sudden-death turn simply repeats.
            if (leftDead) return Resolution(TurnResolution.RepeatSuddenDeathTurn);

            // Idle players still forfeit in sudden death (§3, Decided). Two forfeits at once repeat the turn.
            if (TryForfeit(state, settings, TurnResolution.RepeatSuddenDeathTurn, out EndCheckResult forfeit)) return forfeit;

            return Resolution(TurnResolution.RepeatSuddenDeathTurn);
        }

        /// <param name="whenNoWinner">What a forfeit by both sides with no winner resolves to in this kind of turn.</param>
        private static bool TryForfeit(MatchState state, RulesSettings settings, TurnResolution whenNoWinner,
            out EndCheckResult result)
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
