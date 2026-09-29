namespace XRim.Rules.Combat
{
    public readonly struct BlockResult
    {
        /// <summary>0 = full block, 1 = no reduction.</summary>
        public float DamageMultiplier { get; }

        public bool AttackStopped { get; }
        public bool ShieldHolderStaggered { get; }

        public BlockResult(float damageMultiplier, bool attackStopped, bool shieldHolderStaggered)
        {
            DamageMultiplier = damageMultiplier;
            AttackStopped = attackStopped;
            ShieldHolderStaggered = shieldHolderStaggered;
        }
    }
}
