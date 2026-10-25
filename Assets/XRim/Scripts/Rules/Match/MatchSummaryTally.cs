using System.Collections.Generic;
using XRim.Core;
using XRim.Core.Gdd;
using XRim.Rules.Events;

namespace XRim.Rules.Match
{
    /// <summary>
    /// Counts what a match's events say into a <see cref="MatchSummary"/> (GDD §16: the only thing Economy sees of a match). Feed it each
    /// resolved turn's events once, in order, then ask for the summary when the match ends. It only reads events; nothing in the rules
    /// ever reads it back.
    /// </summary>
    [GddTbd("§16", "What counts as a successful parry", Proposal = "Designer 2026-10-25 (placeholder reading): a shield block, full or partial")]
    public sealed class MatchSummaryTally
    {
        private readonly PerSide<int> _hits = new PerSide<int>(0, 0);
        private readonly PerSide<int> _parries = new PerSide<int>(0, 0);

        /// <summary>Hits landed so far: a <see cref="HitLandedEvent"/> counts for the side that struck.</summary>
        public PerSide<int> HitsLanded => new PerSide<int>(_hits.Left, _hits.Right);

        /// <summary>Parries so far: a <see cref="ShieldBlockEvent"/> counts for the side holding the shield.</summary>
        public PerSide<int> SuccessfulParries => new PerSide<int>(_parries.Left, _parries.Right);

        public void Add(MatchEvent matchEvent)
        {
            Guard.NotNull(matchEvent, nameof(matchEvent));
            if (matchEvent is HitLandedEvent hit) _hits[hit.Hit.Attacker]++;
            else if (matchEvent is ShieldBlockEvent block) _parries[block.Blocker]++;
        }

        public void AddTurn(IEnumerable<MatchEvent> events)
        {
            foreach (MatchEvent matchEvent in Guard.NotNull(events, nameof(events)))
            {
                Add(matchEvent);
            }
        }

        /// <summary>The summary of the match so far; later events do not change a summary already made.</summary>
        public MatchSummary ToSummary(MatchOutcome outcome) => new MatchSummary(outcome, HitsLanded, SuccessfulParries);
    }
}
