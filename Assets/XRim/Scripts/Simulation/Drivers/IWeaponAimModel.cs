using XRim.Core;
using XRim.Simulation.Physics;

namespace XRim.Simulation.Drivers
{
    /// <summary>
    /// How the held item is oriented while its tip follows the path. The GDD does not say; the designer chose
    /// "aim from the shoulder" on 2026-09-29 (<see cref="AimFromShoulderModel"/>), judged by feel. Another model can
    /// replace it without touching the drivers.
    /// </summary>
    public interface IWeaponAimModel
    {
        /// <summary>
        /// The held item's pose in the torso frame for a tip position: position = the grip (hand), rotation = the
        /// direction from grip to tip.
        /// </summary>
        BodyPose Aim(Vec2 tipLocal, Vec2 shoulderLocal, float armLengthUnits, float weaponLengthUnits);
    }
}
