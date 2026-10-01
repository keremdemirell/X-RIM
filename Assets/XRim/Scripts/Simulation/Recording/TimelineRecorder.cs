using System.Collections.Generic;
using XRim.Core;
using XRim.Rules.Events;
using XRim.Simulation.Physics;

namespace XRim.Simulation.Recording
{
    public sealed class TimelineRecorder
    {
        private readonly List<TimelineFrame> _frames = new List<TimelineFrame>();
        private readonly List<MatchEvent> _events = new List<MatchEvent>();

        public void RecordFrame(int step, SimTime time, PoseSnapshot pose) => _frames.Add(new TimelineFrame(step, time, pose));

        public void RecordEvent(MatchEvent matchEvent) => _events.Add(Guard.NotNull(matchEvent, nameof(matchEvent)));

        /// <summary>
        /// Events come out in time order, which playback needs. The engine reports a contact one step after the motion
        /// that made it, so an event can be recorded after one that happened later; equal times keep the recorded order.
        /// </summary>
        public TurnTimeline Build() => new TurnTimeline(_frames.ToArray(), SortedByTime(_events));

        private static MatchEvent[] SortedByTime(List<MatchEvent> events)
        {
            MatchEvent[] sorted = events.ToArray();
            for (int i = 1; i < sorted.Length; i++)
            {
                MatchEvent current = sorted[i];
                int j = i - 1;
                while (j >= 0 && sorted[j].Time > current.Time)
                {
                    sorted[j + 1] = sorted[j];
                    j--;
                }

                sorted[j + 1] = current;
            }

            return sorted;
        }
    }
}
