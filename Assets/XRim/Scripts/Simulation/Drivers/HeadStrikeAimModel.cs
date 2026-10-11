using XRim.Core;
using XRim.Simulation.Physics;

namespace XRim.Simulation.Drivers
{
    /// <summary>
    /// The armless headbutt (GDD §12; designer 2026-10-10, the recommended default: the drawn path steers the head). The drawn point
    /// is where the head's centre goes. The body leans into it (<see cref="HeadbuttLean"/>; the simulation leans the root to match), so
    /// the head tilts with the torso: its pivot at the neck sits one head radius back along the leaning torso's axis. The pose is the
    /// striking head as a held item (<see cref="HeldItemShape.HeadAsItem"/>: +X from the neck to the crown, which mirrors correctly to
    /// either side). No arm is involved, so the shoulder and arm length are unused.
    /// </summary>
    public sealed class HeadStrikeAimModel : IWeaponAimModel
    {
        private readonly float _headRadiusUnits;
        private readonly float _torsoHeightUnits;

        /// <param name="headRadiusUnits">Half the striking head's width (the body blow's ink thickness).</param>
        /// <param name="torsoHeightUnits">Pelvis to neck, for how far the body must lean.</param>
        public HeadStrikeAimModel(float headRadiusUnits, float torsoHeightUnits)
        {
            _headRadiusUnits = headRadiusUnits;
            _torsoHeightUnits = torsoHeightUnits;
        }

        public BodyPose Aim(Vec2 tipLocal, Vec2 shoulderLocal, float armLengthUnits, float weaponLengthUnits)
        {
            HeadbuttLean.Solve(tipLocal, _torsoHeightUnits, _headRadiusUnits, out float lean, out float shift);
            Vec2 axis = HeadbuttLean.AxisLocal(lean);
            Vec2 centre = new Vec2(shift, 0f) + axis * (_torsoHeightUnits + _headRadiusUnits);
            return new BodyPose(centre - axis * _headRadiusUnits, HeldItemShape.HeadItemRotationOffsetDegrees - lean);
        }
    }
}
