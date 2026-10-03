using XRim.Core;
using XRim.Simulation.Physics;

namespace XRim.Simulation.Drivers
{
    /// <summary>
    /// How the held item is oriented while its path point follows the path. The GDD does not say; the designer chose
    /// "aim from the shoulder" for weapons on 2026-09-29 (<see cref="AimFromShoulderModel"/>) and "held like a shield" for the
    /// shield on 2026-10-03 (<see cref="ShieldFaceAimModel"/>), judged by feel. Another model can replace either without
    /// touching the drivers.
    /// </summary>
    public interface IWeaponAimModel
    {
        /// <summary>
        /// The held item's pose in the torso frame for a path point (a weapon's tip, the shield's centre): position = the grip
        /// (hand), rotation = the body's rotation (a weapon: from grip to tip; the shield: the way its face looks).
        /// </summary>
        BodyPose Aim(Vec2 tipLocal, Vec2 shoulderLocal, float armLengthUnits, float weaponLengthUnits);
    }
}
