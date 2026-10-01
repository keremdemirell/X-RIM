using XRim.Core;
using XRim.Rules.Settings;
using XRim.Simulation.Physics;

namespace XRim.Simulation.Drivers
{
    /// <summary>
    /// No body move: the root stays where the turn started (GDD §3: an idle dummy holds its pose). Until Session 05 adds
    /// crouch, lunge, step back and jump, every planned move plays as this one.
    /// </summary>
    public sealed class NeutralBodyMoveDriver : IBodyMoveDriver
    {
        private BodyPose _root;

        public void Begin(Side side, BodyMoveStats move, BodyPose startRoot) => _root = startRoot;

        public BodyPose EvaluateRoot(SimTime time) => _root;

        public bool IsComplete(SimTime time) => true;
    }
}
