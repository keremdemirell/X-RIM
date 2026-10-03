namespace XRim.Rules.Combat
{
    /// <summary>What a shield did to a weapon that met it (GDD §7).</summary>
    public readonly struct BlockResult
    {
        /// <summary>A full block: the weapon's attack stops at the shield.</summary>
        public bool AttackStopped { get; }

        /// <summary>
        /// The share of its damage the weapon still deals with its later hits this turn: 0 for a full block, the partial reduction
        /// otherwise (A5).
        /// </summary>
        public float DamageMultiplier { get; }

        /// <summary>The shield holder is staggered (a D21 option).</summary>
        public bool ShieldHolderStaggered { get; }

        public BlockResult(bool attackStopped, float damageMultiplier, bool shieldHolderStaggered)
        {
            AttackStopped = attackStopped;
            DamageMultiplier = damageMultiplier;
            ShieldHolderStaggered = shieldHolderStaggered;
        }
    }
}
