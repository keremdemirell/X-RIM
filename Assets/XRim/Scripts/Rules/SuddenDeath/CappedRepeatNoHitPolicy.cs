using XRim.Core;
using XRim.Core.Gdd;
using XRim.Rules.Match;
using XRim.Rules.Settings;

namespace XRim.Rules.SuddenDeath
{
    /// <summary>
    /// The designer's default (2026-10-11, GDD §14 TBD): a sudden-death turn without a winner is played again, at most
    /// <see cref="SuddenDeathSettings.MaxRepeatsWithoutHit"/> times. Then the dummy that had more HP when sudden death began wins
    /// (<see cref="MatchEndReason.SuddenDeathMoreHp"/>; after a double KO both had 0); with equal HP a coin toss from the match seed decides
    /// (<see cref="MatchEndReason.SuddenDeathCoinToss"/>), the same toss for the same match. Both fallbacks bend §3's "no points win", so
    /// they are flagged.
    /// </summary>
    [GddTbd("§14", "Nobody hits in sudden death: the fallback after the cap",
        Proposal = "More HP when sudden death began, then a coin toss from the match seed")]
    public sealed class CappedRepeatNoHitPolicy : ISuddenDeathNoHitPolicy
    {
        public SuddenDeathNoHitDecision Decide(MatchState state, SuddenDeathSettings settings, uint matchSeed)
        {
            Guard.NotNull(state, nameof(state));
            Guard.NotNull(settings, nameof(settings));
            if (state.SuddenDeathRepeats < settings.MaxRepeatsWithoutHit) return SuddenDeathNoHitDecision.Repeat;

            float left = state.Fighters[Side.Left].HpBeforeSuddenDeath;
            float right = state.Fighters[Side.Right].HpBeforeSuddenDeath;
            if (left != right) return SuddenDeathNoHitDecision.Win(left > right ? Side.Left : Side.Right, MatchEndReason.SuddenDeathMoreHp);

            Side tossed = new XorShiftRandom(matchSeed).NextInt(0, 2) == 0 ? Side.Left : Side.Right;
            return SuddenDeathNoHitDecision.Win(tossed, MatchEndReason.SuddenDeathCoinToss);
        }
    }
}
