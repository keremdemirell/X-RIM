using XRim.Core;
using XRim.Simulation.Physics;

namespace XRim.Simulation.Drivers
{
    /// <summary>
    /// Designer, 2026-10-03 (A3, not in the GDD): the shield is held like a shield. The drawn point is where its centre goes:
    /// the hand reaches toward it along the line from the shoulder, as far as the arm allows, and the face turns outward, square
    /// to that line, so the whole height of the shield stands in front of the arm. An arc sweeps it like a parry; a tap raises
    /// it where it was tapped. The shield adds no reach of its own (<c>WeaponStats.ReachBeyondHandUnits</c>).
    /// </summary>
    public sealed class ShieldFaceAimModel : IWeaponAimModel
    {
        /// <param name="tipLocal">The drawn point: where the shield's centre goes.</param>
        /// <param name="weaponLengthUnits">Unused: the shield is held at its centre.</param>
        public BodyPose Aim(Vec2 tipLocal, Vec2 shoulderLocal, float armLengthUnits, float weaponLengthUnits)
        {
            Vec2 offset = tipLocal - shoulderLocal;
            float distance = offset.Length;
            Vec2 direction = distance > 0f ? offset / distance : Vec2.UnitX;
            float hand = XMath.Clamp(distance, 0f, armLengthUnits);
            return new BodyPose(shoulderLocal + direction * hand, direction.AngleDegrees);
        }
    }
}
