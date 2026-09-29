using XRim.Core;
using XRim.Simulation.Physics;

namespace XRim.Simulation.Execution
{
    /// <summary>A body's recorded pose at one simulation time.</summary>
    public readonly struct PoseSample
    {
        public SimTime Time { get; }
        public BodyPose Pose { get; }

        public PoseSample(SimTime time, BodyPose pose)
        {
            Time = time;
            Pose = pose;
        }
    }
}
