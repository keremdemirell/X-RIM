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

        public TurnTimeline Build() => new TurnTimeline(_frames.ToArray(), _events.ToArray());
    }
}
