using XRim.Core;
using XRim.Simulation.Physics;

namespace XRim.Simulation.Drivers
{
    /// <summary>
    /// Designer, 2026-09-29: the weapon lies on the line from the shoulder through the path point, like an arm and
    /// blade thrown out as one piece. The hand slides along that line, within arm reach:
    /// far points extend the arm, close points pull the hand back (behind the shoulder if needed).
    /// The tip lands exactly on the path wherever the path is reachable. When a point is closer to the shoulder than
    /// the weapon's length minus the arm's, the weapon still points through it but the tip overshoots.
    /// </summary>
    public sealed class AimFromShoulderModel : IWeaponAimModel
    {
        public BodyPose Aim(Vec2 tipLocal, Vec2 shoulderLocal, float armLengthUnits, float weaponLengthUnits)
        {
            Vec2 offset = tipLocal - shoulderLocal;
            float distance = offset.Length;
            Vec2 direction = distance > 0f ? offset / distance : Vec2.UnitX;
            float handAlongLine = XMath.Clamp(distance - weaponLengthUnits, -armLengthUnits, armLengthUnits);
            return new BodyPose(shoulderLocal + direction * handAlongLine, direction.AngleDegrees);
        }
    }
}
