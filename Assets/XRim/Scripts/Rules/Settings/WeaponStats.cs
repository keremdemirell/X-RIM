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

        /// <summary>
        /// Swing armour: a hit does not interrupt this weapon's attack while it travels its path (a killing hit still does).
        /// D16 default: off for every weapon, to try at PT2.
        /// </summary>
        [GddTbd("§9", "Do heavy weapons resist interruption?", Proposal = "D16 default: off, a toggle per weapon")]
        public bool HasSwingArmour;

        /// <summary>
        /// D26 (designer, 2026-10-03): after a hit the weapon keeps going along its path at this share of its speed (0 = it
        /// stops), and its next hit deals damage scaled by the speed it has left. Heavy weapons plough through, light ones
        /// stick.
        /// </summary>
        [Placeholder("D26: the speed a weapon keeps after a hit has no value")]
        public float SpeedKeptAfterHitFraction;

        /// <summary>Shield bash knockback (GDD §7). Unused by other kinds.</summary>
        [Placeholder("§7 bash knockback is Tunable with no value")]
        public float KnockbackImpulse;

        [GddTbd("§15", "Lifesteal and build-up details, and which weapons carry them")]
        public List<string> TraitIds = new List<string>();

        public WeaponId WeaponId => new WeaponId(Id);

        /// <summary>
        /// This weapon with its ink length scaled, for effects that change the ink budget
        /// (<c>PlanningConstraints.InkLengthMultiplier</c>). A shallow copy that shares the rigidity and trait
        /// data with the original, so treat it as read-only. Returns this weapon when the multiplier is 1.
        /// </summary>
        public WeaponStats WithInkLengthMultiplier(float multiplier)
        {
            if (multiplier == 1f) return this;
            var copy = (WeaponStats)MemberwiseClone();
            copy.InkLengthUnits = InkLengthUnits * multiplier;
            return copy;
        }

        /// <summary>
        /// This weapon travelling its path faster or slower for one turn (a body move's speed bonus, D13 TBD §5). The same
        /// read-only shallow copy as <see cref="WithInkLengthMultiplier"/>; returns this weapon when the multiplier is 1.
        /// </summary>
        public WeaponStats WithSpeedMultiplier(float multiplier)
        {
            if (multiplier == 1f) return this;
            var copy = (WeaponStats)MemberwiseClone();
            copy.SpeedUnitsPerSecond = SpeedUnitsPerSecond * multiplier;
            return copy;
        }
    }
}
