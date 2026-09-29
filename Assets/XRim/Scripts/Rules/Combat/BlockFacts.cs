using XRim.Rules.Settings;

namespace XRim.Rules.Combat
{
    /// <summary>A weapon touching a shield. Shields protect only the area they occupy at that instant (GDD §7).</summary>
    public readonly struct BlockFacts
    {
        /// <summary>90° = square on the shield face; low angles = edge or glancing hits.</summary>
        public float ContactAngleDegrees { get; }

        public WeaponStats AttackerWeapon { get; }
        public float AttackerSpeedUnitsPerSecond { get; }
        public WeaponStats Shield { get; }

        public BlockFacts(float contactAngleDegrees, WeaponStats attackerWeapon, float attackerSpeedUnitsPerSecond, WeaponStats shield)
        {
            ContactAngleDegrees = contactAngleDegrees;
            AttackerWeapon = attackerWeapon;
            AttackerSpeedUnitsPerSecond = attackerSpeedUnitsPerSecond;
            Shield = shield;
        }
    }
}
