using System;
using System.Globalization;
using XRim.Core;
using XRim.Rules;
using XRim.Rules.Match;

namespace XRim.App
{
    /// <summary>
    /// The match-over screen's words: who won, how, and how long it took (GDD §3: a match ends only on a KO or a forfeit; §14: sudden death
    /// always ends, by a hit or, once its tries run out, by the designer's fallback).
    /// </summary>
    public static class MatchOverText
    {
        private const int SecondsPerMinute = 60;

        public static string Title(MatchOutcome outcome, PerSide<string> names)
        {
            Guard.NotNull(outcome, nameof(outcome));
            string winner = names[outcome.Winner];
            return winner == MatchLabels.You ? "YOU WIN!" : winner + " WINS!";
        }

        public static string Reason(MatchOutcome outcome, PerSide<string> names)
        {
            Guard.NotNull(outcome, nameof(outcome));
            switch (outcome.Reason)
            {
                case MatchEndReason.KnockOut:
                    return "by knockout";
                case MatchEndReason.Forfeit:
                    return string.Format(CultureInfo.InvariantCulture, "by forfeit: {0} set no move for {1} turns in a row",
                        names[outcome.Winner.Opponent()], RuleConstants.IdleTurnsBeforeForfeit);
                case MatchEndReason.SuddenDeathHit:
                    return "with the first hit in sudden death";
                case MatchEndReason.SuddenDeathMoreHp:
                    return "sudden death ran out of tries: more HP when it began";
                case MatchEndReason.SuddenDeathCoinToss:
                    return "sudden death ran out of tries, level on HP: a coin toss";
                default:
                    throw new ArgumentOutOfRangeException(nameof(outcome), outcome.Reason, "Unknown end reason.");
            }
        }

        /// <summary>For example "7 turns · 2:05" (real time, planning and playback included).</summary>
        public static string Summary(int turnCount, double realSeconds)
        {
            int total = (int)Math.Round(Math.Max(realSeconds, 0.0));
            string turns = turnCount == 1 ? "1 turn" : turnCount.ToString(CultureInfo.InvariantCulture) + " turns";
            return string.Format(CultureInfo.InvariantCulture, "{0} · {1}:{2:00}", turns, total / SecondsPerMinute, total % SecondsPerMinute);
        }
    }
}
