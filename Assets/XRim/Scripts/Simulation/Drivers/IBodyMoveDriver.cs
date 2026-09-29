using XRim.Core;
using XRim.Rules.Settings;
using XRim.Simulation.Physics;

namespace XRim.Simulation.Drivers
{
    /// <summary>Plays a body move alongside the weapon path, starting when execution starts (GDD §5, Decided).</summary>
    public interface IBodyMoveDriver
    {
        void Begin(Side side, BodyMoveStats move, BodyPose startRoot);

        BodyPose EvaluateRoot(SimTime time);

        bool IsComplete(SimTime time);
    }
}
