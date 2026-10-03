namespace XRim.Rules.Damage
{
    /// <summary>What one landed hit does (GDD §11). The calculator only computes it; applying it changes the fighter.</summary>
    public readonly struct DamageResult
    {
        /// <summary>The hit's damage: BaseDamage × ZoneMultiplier × Modifiers. The stun threshold reads this.</summary>
        public float Damage { get; }

        /// <summary>HP actually removed: <see cref="Damage"/>, unless the no-instant-KO rule (D27) clamped it.</summary>
        public float HpDamage { get; }

        /// <summary>Damage added to the limb's own total after the per-hit cap (GDD §11). 0 for head and torso.</summary>
        public float LimbDamage { get; }

        /// <summary>A single head hit above the stun threshold stuns (GDD §11); never an instant KO.</summary>
        public bool Stuns { get; }

        /// <summary>
        /// This hit brought the limb's damage to its durability (GDD §11). Session 11 severs the limb; until then it is
        /// only reported.
        /// </summary>
        public bool SeversLimb { get; }

        public bool WasClampedByNoInstantKo => HpDamage < Damage;

        public DamageResult(float damage, float hpDamage, float limbDamage, bool stuns, bool seversLimb)
        {
            Damage = damage;
            HpDamage = hpDamage;
            LimbDamage = limbDamage;
            Stuns = stuns;
            SeversLimb = seversLimb;
        }
    }
}
