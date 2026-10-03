using XRim.Core;

namespace XRim.Rules.Combat
{
    /// <summary>One weapon's side of a clash (GDD §10).</summary>
    public readonly struct ClashParticipant
    {
        public Side Side { get; }
        public float Mass { get; }

        /// <summary>
        /// The v of P = W_m·m + W_v·v: the speed the rules move the weapon along its path at the contact (designer, 2026-10-03,
        /// A1): its speed stat with a body move's bonus (D13), times the share it kept after its hits (D26); 0 for a weapon
        /// that is not travelling its path. Never physics velocity (§9: speed is a weapon stat).
        /// </summary>
        public float SpeedUnitsPerSecond { get; }

        public ClashParticipant(Side side, float mass, float speedUnitsPerSecond)
        {
            Side = side;
            Mass = mass;
            SpeedUnitsPerSecond = speedUnitsPerSecond;
        }
    }
}
