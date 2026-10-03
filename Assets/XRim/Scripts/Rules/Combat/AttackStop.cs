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

        /// <summary>Knocked off its path by a clash (§10): it lost a crush-through, or a lighter weapon deflected it.</summary>
        KnockedOff = 4,

        /// <summary>A hard clash between close powers (§10): it rebounded in sparks.</summary>
        Rebounded = 5,

        /// <summary>A shield fully blocked it (§7).</summary>
        Blocked = 6,
    }
}
