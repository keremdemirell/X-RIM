namespace XRim.Simulation.Drivers
{
    /// <summary>Why the rules stopped a weapon on its path.</summary>
    public enum WeaponStopKind
    {
        /// <summary>It landed its last allowed hit (D26): it recoils back along its path.</summary>
        LastHit = 0,

        /// <summary>Its attack was cancelled (§9 interrupt, or its dummy died): it is held where it was.</summary>
        Interrupted = 1,

        /// <summary>A clash knocked it off its path (§10): it flies free with a kick, then is held wherever it ended up.</summary>
        KnockedOff = 2,

        /// <summary>A hard clash or a full shield block bounced it back (§7, §10): it rebounds, then is held backed off by the recoil.</summary>
        Rebounded = 3,
    }
}
