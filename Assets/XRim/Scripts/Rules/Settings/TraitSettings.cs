using System;
using XRim.Core.Gdd;
using XRim.Rules.Damage;

namespace XRim.Rules.Settings
{
    /// <summary>
    /// Weapon traits (GDD §15): in-match effects that only a specific weapon carries, never a default dummy ability. Everything about
    /// them is TBD. Designer 2026-10-15 (the recommended default, "seams only"): both traits are built behind a flag, off, and no weapon
    /// carries one (<see cref="WeaponStats.TraitIds"/> is empty), so nothing changes in a duel until a trait is switched on and given
    /// to a weapon. A weapon that carries a trait must still be a sidegrade, not stronger overall (§15, §16). A low-HP rage multiplier
    /// is Rejected: build-up scales with the damage taken, never with the HP left.
    /// </summary>
    [Serializable]
    public sealed class TraitSettings
    {
        [GddTbd("§15", "Do weapon traits appear in this build at all", Proposal = "Seams only: off, no weapon carries a trait (designer 2026-10-15)")]
        public bool LifestealEnabled;

        [GddTbd("§15", "Do weapon traits appear in this build at all", Proposal = "Seams only: off, no weapon carries a trait (designer 2026-10-15)")]
        public bool BuildUpEnabled;

        /// <summary>GDD §15: "After 3 successful hits".</summary>
        public int LifestealHitsNeeded = 3;

        [GddTbd("§15", "Lifesteal: consecutive or total hits", Proposal = "Total hits; consecutive stays switchable (designer 2026-10-15)")]
        public LifestealHitCounting LifestealHitCounting = LifestealHitCounting.Total;

        /// <summary>The share of the damage those hits dealt that the dummy recovers when the count is reached.</summary>
        [GddTbd("§15", "Lifesteal: the recovery percentage", Proposal = "30% (placeholder)")]
        [Placeholder("§15 recovery percentage has no value")]
        public float LifestealRecoveredFraction = 0.3f;

        /// <summary>Build-up: each point of HP the dummy has lost in total raises its damage by this share of its base damage.</summary>
        [GddTbd("§15", "Build-up: the growth curve", Proposal = "Linear, +0.5% per HP of damage taken (placeholder)")]
        [Placeholder("§15 growth curve has no value")]
        public float BuildUpGrowthPerHpDamageTaken = 0.005f;

        /// <summary>Build-up never raises damage by more than this share of its base ("up to a limit").</summary>
        [GddTbd("§15", "Build-up: the cap", Proposal = "+50% (placeholder)")]
        [Placeholder("§15 cap has no value")]
        public float BuildUpMaxBonusFraction = 0.5f;
    }
}
