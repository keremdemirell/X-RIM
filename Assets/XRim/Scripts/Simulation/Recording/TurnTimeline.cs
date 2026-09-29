using System.Collections.Generic;
using XRim.Core;
using XRim.Rules.Events;

namespace XRim.Simulation.Recording
{
    /// <summary>
    /// The recording of one executed turn: poses over time plus time-stamped domain events. Presentation plays it
    /// back at any speed (slow motion, scrubbing, replays) without re-simulating, so feel effects never change outcomes.
    /// </summary>
    public sealed class TurnTimeline
    {
        public IReadOnlyList<TimelineFrame> Frames { get; }
        public IReadOnlyList<MatchEvent> Events { get; }

        public TurnTimeline(IReadOnlyList<TimelineFrame> frames, IReadOnlyList<MatchEvent> events)
        {
            Frames = Guard.NotNull(frames, nameof(frames));
            Events = Guard.NotNull(events, nameof(events));
        }

        public SimTime Duration => Frames.Count > 0 ? Frames[Frames.Count - 1].Time : SimTime.Zero;
    }
}
