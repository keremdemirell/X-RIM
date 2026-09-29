using System;
using XRim.Core.Gdd;

namespace XRim.Rules.Settings
{
    /// <summary>HP, limb durability and damage modifiers (GDD §11, §12).</summary>
    [Serializable]
    public sealed class DamageSettings
    {
        [Placeholder("§11 HP scale is TBD")]
        public float MaxHp = 100f;

        /// <summary>GDD §11, Tunable: one hit adds at most this fraction of a limb's durability (so severing needs 3+ hits).</summary>
        public float PerHitLimbCapFraction = 0.35f;

        [Placeholder("§11 limb durability is TBD; arms and legs may need different values")]
        public float ArmDurability = 40f;

        [Placeholder("§11 limb durability is TBD; arms and legs may need different values")]
        public float LegDurability = 50f;

        /// <summary>GDD §12, Tunable: damage multiplier when fighting with the off hand.</summary>
        public float OffHandDamageMultiplier = 0.8f;

        [Placeholder("§11 head stun threshold is TBD until the HP scale is set")]
        public float HeadStunThreshold = 30f;
    }
}
