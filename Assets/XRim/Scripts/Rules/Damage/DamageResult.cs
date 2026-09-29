namespace XRim.Rules.Damage
{
    public readonly struct DamageResult
    {
        public float HpDamage { get; }

        /// <summary>Damage added to the limb's own total after the per-hit cap (GDD §11). 0 for head and torso.</summary>
        public float LimbDamage { get; }

        /// <summary>A single head hit above the stun threshold stuns (GDD §11); never an instant KO.</summary>
        public bool Stuns { get; }

        public bool SeversLimb { get; }

        public DamageResult(float hpDamage, float limbDamage, bool stuns, bool seversLimb)
        {
            HpDamage = hpDamage;
            LimbDamage = limbDamage;
            Stuns = stuns;
            SeversLimb = seversLimb;
        }
    }
}
