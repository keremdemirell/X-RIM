using XRim.Rules.Settings;

namespace XRim.Rules.Combat
{
    /// <summary>
    /// A travelling weapon meeting a shield, measured by the simulation from where the shield actually is at that instant
    /// (GDD §7: it protects only the area it occupies).
    /// </summary>
    public readonly struct BlockFacts
    {
        /// <summary>The angle between the weapon's motion relative to the shield and the shield face: 90° = straight into the face.</summary>
        public float ContactAngleDegrees { get; }

        /// <summary>
        /// Where along the face the weapon met it, from its centre: 0 = the middle, 1 = the end of the face, more = past it (on
        /// the end itself).
        /// </summary>
        public float FacePositionFraction { get; }

        public WeaponStats AttackerWeapon { get; }

        /// <summary>The speed the rules move the weapon along its path at the contact (as in a clash, A1).</summary>
        public float AttackerSpeedUnitsPerSecond { get; }

        public WeaponStats Shield { get; }

        public BlockFacts(float contactAngleDegrees, float facePositionFraction, WeaponStats attackerWeapon, float attackerSpeedUnitsPerSecond,
            WeaponStats shield)
        {
            ContactAngleDegrees = contactAngleDegrees;
            FacePositionFraction = facePositionFraction;
            AttackerWeapon = attackerWeapon;
            AttackerSpeedUnitsPerSecond = attackerSpeedUnitsPerSecond;
            Shield = shield;
        }
    }
}
