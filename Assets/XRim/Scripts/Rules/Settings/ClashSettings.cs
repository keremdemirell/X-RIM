using System;
using XRim.Core.Gdd;

namespace XRim.Rules.Settings
{
    /// <summary>Two-stage clash model (GDD §10). The model is Decided; every threshold and weight is Tunable.</summary>
    [Serializable]
    public sealed class ClashSettings
    {
        /// <summary>Stage 1: contact angle at or above this is a hard clash, below it a glancing contact.</summary>
        public float HardClashAngleDegrees = 30f;

        /// <summary>Stage 2 (hard clash): stronger/weaker power at or above this crushes through.</summary>
        public float CrushRatio = 1.5f;

        /// <summary>A weapon that crushes through continues with this damage multiplier (Appendix A: -30%).</summary>
        public float CrushThroughDamageMultiplier = 0.7f;

        /// <summary>
        /// Clash power P = W_m·m + W_v·v. The designer wants the weights to differ. Mass and speed use different
        /// units (mass vs arena units/s), so the raw numbers are not comparable to each other.
        /// </summary>
        [Placeholder("§10 W_m and W_v must be unequal; values open")]
        public float MassWeight = 1f;

        [Placeholder("§10 W_m and W_v must be unequal; values open")]
        public float SpeedWeight = 0.005f;

        /// <summary>
        /// Glancing contact: when the heavier weapon is at most this fraction heavier than the lighter (edge included), both
        /// slide past and continue; otherwise the lighter deflects the heavier.
        /// </summary>
        [Placeholder("§10 the similar-mass band is Tunable with no value")]
        public float GlancingSimilarMassBandFraction = 0.2f;

        /// <summary>
        /// D19: how many contacts between the two held items the rules resolve in one turn; later ones pass through (physics
        /// only), so two blades grinding together never loop. Weapon-to-weapon and weapon-to-shield contacts share the count.
        /// 0 = every contact.
        /// </summary>
        [GddTbd("§10", "Multiple contacts between two weapons in one turn",
            Proposal = "D19 default (designer, 2026-10-03): only the first contact resolves")]
        public int MaxResolvedContactsPerWeaponPair = 1;

        /// <summary>A weapon meeting a shield uses the shield's own block rule, never the clash model (GDD §7, §10).</summary>
        public ShieldBlockSettings ShieldBlock = new ShieldBlockSettings();
    }
}
