namespace XRim.Rules.Signature
{
    /// <summary>The ids of the special properties in the effect catalogue (GDD §8), as a move's <c>SpecialEffectId</c> names them.</summary>
    public static class SignatureEffectIds
    {
        /// <summary>The victim is knocked back much farther ("5 Finger Exploding Touch").</summary>
        public const string KnockbackBurst = "knockback_burst";

        /// <summary>A landed hit stuns ("Head Impact Simulation #404").</summary>
        public const string StunOnHit = "stun_on_hit";

        /// <summary>The move's hits deal more damage ("Whiplash Calibration Spin").</summary>
        public const string DamageBoost = "damage_boost";
    }
}
