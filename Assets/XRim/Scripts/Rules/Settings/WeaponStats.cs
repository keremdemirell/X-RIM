using System;
using System.Collections.Generic;
using XRim.Core.Gdd;

namespace XRim.Rules.Settings
{
    /// <summary>
    /// One weapon, shield or severed-limb club (GDD §6). Identity comes from the ink budget
    /// (length × thickness) plus mass, speed and damage. Every number is Tunable.
    /// </summary>
    [Serializable]
    public sealed class WeaponStats
    {
        public string Id = string.Empty;
        public string DisplayName = string.Empty;
        public WeaponKind Kind = WeaponKind.Weapon;

        /// <summary>True when the GDD marks this weapon's design TBD, so every number on it is a placeholder.</summary>
        public bool DesignIsTbd;

        /// <summary>How far the path can travel, in arena units.</summary>
        public float InkLengthUnits;

        /// <summary>The weapon's hit width along the path, in arena units.</summary>
        public float InkThicknessUnits;

        /// <summary>
        /// Weapon length from the hand to the tip, arena units. Adds to the arm length for the reach limit (GDD §6).
        /// </summary>
        [Placeholder("§6 weapon lengths are not given")]
        public float LengthUnits;

        public float Mass;

        /// <summary>
        /// GDD §9 (Decided model): t_impact = distance along path / speed. Only the order is set:
        /// rapier fast, mace slow.
        /// </summary>
        [Placeholder("§6/§9 speed values are TBD; only the order rapier > mace is set")]
        public float SpeedUnitsPerSecond;

        /// <summary>GDD §11: designer-set base damage, never physics energy.</summary>
        [Placeholder("§11 base damage values are TBD")]
        public float BaseDamage;

        public RigiditySettings Rigidity = new RigiditySettings();

        [GddTbd("§9", "Do heavy weapons resist interruption?")]
        public bool HasSwingArmour;

        /// <summary>Shield bash knockback (GDD §7). Unused by other kinds.</summary>
        [Placeholder("§7 bash knockback is Tunable with no value")]
        public float KnockbackImpulse;

        [GddTbd("§15", "Lifesteal and build-up details, and which weapons carry them")]
        public List<string> TraitIds = new List<string>();

        public WeaponId WeaponId => new WeaponId(Id);
    }
}
