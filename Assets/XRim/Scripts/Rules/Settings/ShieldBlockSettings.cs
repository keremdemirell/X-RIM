using System;
using XRim.Core.Gdd;
using XRim.Rules.Combat;

namespace XRim.Rules.Settings
{
    /// <summary>
    /// The shield's own block rule (GDD §7): weapon-to-shield contacts never use the clash model. Lives in the Clash asset with
    /// the other weapon-contact rules (designer, 2026-10-03, A8).
    /// </summary>
    [Serializable]
    public sealed class ShieldBlockSettings
    {
        /// <summary>Full block, damage reduction, or both depending on where and how the shield is hit (D20).</summary>
        [GddTbd("§7", "Shield block model",
            Proposal = "D20 default (designer, 2026-10-03): full block square-on to the face, partial at the rim or glancing")]
        public ShieldBlockRule Rule = ShieldBlockRule.SquareFaceFullOtherwisePartial;

        /// <summary>
        /// A partially blocked weapon carries on, and every later hit it lands this turn deals this share of its damage (A5).
        /// </summary>
        [Placeholder("D20: the partial reduction has no value (50% suggested)")]
        public float PartialBlockDamageMultiplier = 0.5f;

        /// <summary>
        /// A hit counts as square-on when the weapon's motion meets the shield face at this angle or more (0° = sliding along
        /// the face, 90° = straight into it); below it, it is glancing.
        /// </summary>
        [Placeholder("§7 'square' and 'glancing' hits have no angle; the clash threshold to start")]
        public float SquareHitMinAngleDegrees = 30f;

        /// <summary>The outer share of the face, at each end, that counts as the shield's rim (an edge hit).</summary>
        [Placeholder("§7 how much of the shield is 'edge' has no value")]
        public float RimFraction = 0.2f;

        /// <summary>What a heavy weapon does against a shield (D21).</summary>
        [GddTbd("§7", "Can a mace break or stagger a shield?",
            Proposal = "D21 default (designer, 2026-10-03): no special rule; the block holds and the holder is pushed back")]
        public HeavyWeaponBlockRule HeavyWeapon = HeavyWeaponBlockRule.None;

        /// <summary>A weapon at least this heavy counts as heavy for <see cref="HeavyWeapon"/> (only the mace with the starting values).</summary>
        [Placeholder("D21: which weapons count as heavy has no value")]
        public float HeavyWeaponMinMass = 10f;
    }
}
