using System;
using XRim.Core.Gdd;
using XRim.Rules.Signature;

namespace XRim.Rules.Settings
{
    /// <summary>
    /// Signature (joker) move rules (GDD §8). Almost everything here is TBD; designer 2026-10-15: "all recommended", the defaults below
    /// as flagged seams.
    /// </summary>
    [Serializable]
    public sealed class SignatureSettings
    {
        [GddTbd("§8", "Signature charge: once per match or Kinetic Energy Bar",
            Proposal = "Once per match; the Kinetic Energy Bar is built too and switchable (designer 2026-10-15)")]
        public SignatureChargeMode ChargeMode = SignatureChargeMode.OncePerMatch;

        /// <summary>
        /// How many signature moves a dummy may use in one match with the once-per-match charge (any move of the catalogue, each use
        /// spends one). The bar ignores it: it is limited only by the bar.
        /// </summary>
        [GddTbd("§8", "Number of signature moves per match", Proposal = "1 (designer 2026-10-15)")]
        [Placeholder("§8 count per match is TBD")]
        public int MovesPerMatch = 1;

        [GddTbd("§8", "Can the opponent see a charged signature move?", Proposal = "No; a chosen move stays hidden (designer 2026-10-15)")]
        public bool OpponentSeesCharged = false;

        /// <summary>How much of the full bar (1) each point of HP damage this dummy deals fills. Bar mode only.</summary>
        [GddTbd("§8", "Signature charge: Kinetic Energy Bar fill rates", Proposal = "0.5% of the bar per HP dealt (placeholder)")]
        [Placeholder("§8 bar fill rates have no value")]
        public float BarChargePerHpDamageDealt = 0.005f;

        /// <summary>
        /// How much of the full bar each point of HP damage this dummy takes fills (wall zaps count). More than the rate for damage dealt,
        /// so being hurt charges faster: comeback swings (§8). Bar mode only.
        /// </summary>
        [GddTbd("§8", "Signature charge: Kinetic Energy Bar fill rates", Proposal = "1% of the bar per HP taken (placeholder)")]
        [Placeholder("§8 bar fill rates have no value")]
        public float BarChargePerHpDamageTaken = 0.01f;

        /// <summary>The knockback burst effect ("5 Finger Exploding Touch"): its hits knock the victim back this many times as far.</summary>
        [GddTbd("§8", "Catalogue of signature moves", Proposal = "Knockback ×3 (placeholder, designer 2026-10-15)")]
        [Placeholder("§8 catalogue numbers have no value")]
        public float KnockbackBurstMultiplier = 3f;

        /// <summary>The damage boost effect ("Whiplash Calibration Spin"): its hits deal this many times the damage.</summary>
        [GddTbd("§8", "Catalogue of signature moves", Proposal = "Damage ×1.5 (placeholder, designer 2026-10-15)")]
        [Placeholder("§8 catalogue numbers have no value")]
        public float DamageBoostMultiplier = 1.5f;
    }
}
