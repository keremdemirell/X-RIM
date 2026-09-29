using System;
using XRim.Config;
using XRim.Core;
using XRim.Presentation.Playback;
using XRim.Rules.Events;

namespace XRim.Presentation.Feel
{
    /// <summary>
    /// Game-feel layer (GDD pillar 1): hitstop, slow motion on severs and KOs, camera punch. It only bends playback
    /// speed and triggers effects, so it can be tuned aggressively without changing outcomes.
    /// </summary>
    public sealed class FeelDirector : IMatchEventReactor
    {
        private FeelConfig Config { get; }
        private TimelinePlayer Player { get; }

        public FeelDirector(FeelConfig config, TimelinePlayer player)
        {
            Config = config != null ? config : throw new ArgumentNullException(nameof(config));
            Player = Guard.NotNull(player, nameof(player));
        }

        public void OnMatchEvent(MatchEvent matchEvent)
        {
            // Placeholder: architecture setup only. Feel effects come later.
            throw new NotImplementedException("FeelDirector.OnMatchEvent is not implemented yet.");
        }
    }
}
