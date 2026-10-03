namespace XRim.Rules.Combat
{
    /// <summary>Why an attack stopped before the end of its path, if it did.</summary>
    public enum AttackStop
    {
        /// <summary>Still going, or it ran its whole path.</summary>
        None = 0,

        /// <summary>It landed its last allowed hit, or had no speed left after a hit (D26): it stops there with a recoil.</summary>
        LastHit = 1,

        /// <summary>A hit on its dummy cancelled it (§9).</summary>
        Interrupted = 2,

        /// <summary>Its dummy died (§11).</summary>
        WielderDied = 3,
    }
}
