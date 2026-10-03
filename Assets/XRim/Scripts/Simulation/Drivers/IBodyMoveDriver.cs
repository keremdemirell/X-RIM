using XRim.Core;
using XRim.Rules.Settings;

namespace XRim.Simulation.Drivers
{
    /// <summary>
    /// Plays a body move alongside the weapon path, starting when execution starts (GDD §5, Decided). Each step it says
    /// where the root (pelvis) and both soles should be; the simulation drives the torso there and bends the legs to match.
    /// </summary>
    public interface IBodyMoveDriver
    {
        /// <param name="move">The move's data; null when the tuning profile has none (the move then holds the pose).</param>
        void Begin(BodyMoveStats move, BodyMoveStart start);

        BodyMoveFrame Evaluate(SimTime time);

        bool IsComplete(SimTime time);
    }
}
