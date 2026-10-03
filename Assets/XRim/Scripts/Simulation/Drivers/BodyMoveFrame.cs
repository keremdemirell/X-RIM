using XRim.Core;
using XRim.Simulation.Physics;

namespace XRim.Simulation.Drivers
{
    /// <summary>Where a body move wants the dummy at one instant: its root (the pelvis) and where each sole should be.</summary>
    public readonly struct BodyMoveFrame
    {
        /// <summary>The root target: pelvis position and torso tilt, arena units and degrees.</summary>
        public BodyPose Root { get; }

        /// <summary>The front leg's sole target, arena units (on the floor unless the move tucks the feet).</summary>
        public Vec2 FrontSoleUnits { get; }

        public Vec2 BackSoleUnits { get; }

        public BodyMoveFrame(BodyPose root, Vec2 frontSoleUnits, Vec2 backSoleUnits)
        {
            Root = root;
            FrontSoleUnits = frontSoleUnits;
            BackSoleUnits = backSoleUnits;
        }
    }
}
