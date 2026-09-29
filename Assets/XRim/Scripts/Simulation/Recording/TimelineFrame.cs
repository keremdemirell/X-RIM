using XRim.Core;
using XRim.Simulation.Physics;

namespace XRim.Simulation.Recording
{
    public sealed class TimelineFrame
    {
        public int Step { get; }
        public SimTime Time { get; }
        public PoseSnapshot Pose { get; }

        public TimelineFrame(int step, SimTime time, PoseSnapshot pose)
        {
            Step = step;
            Time = time;
            Pose = Guard.NotNull(pose, nameof(pose));
        }
    }
}
