using XRim.Core.Gdd;
using XRim.Rules.Match;
using XRim.Rules.Settings;

namespace XRim.Rules.SuddenDeath
{
    /// <summary>
    /// What happens after a sudden-death turn without a winner (GDD §14, TBD): nobody hit, a tie the <see cref="ITiePolicy"/> replays, or both
    /// idle (P6). Nothing may loop for ever, so a policy must end the match after a bounded number of repeats. Default:
    /// <see cref="CappedRepeatNoHitPolicy"/>.
    /// </summary>
    [GddTbd("§14", "Nobody hits in sudden death",
        Proposal = "Repeat the turn, at most MaxRepeatsWithoutHit times; then more HP when sudden death began wins, equal HP a coin toss " +
                   "from the match seed (designer 2026-10-11; both bend §3's 'no points win')")]
    public interface ISuddenDeathNoHitPolicy
    {
        /// <param name="state">The resolved state of the turn that had no winner; <see cref="MatchState.SuddenDeathRepeats"/> counts the repeats so far.</param>
        /// <param name="matchSeed">The match's seed (<see cref="MatchSetup.RandomSeed"/>), for any random choice.</param>
        SuddenDeathNoHitDecision Decide(MatchState state, SuddenDeathSettings settings, uint matchSeed);
    }
}
