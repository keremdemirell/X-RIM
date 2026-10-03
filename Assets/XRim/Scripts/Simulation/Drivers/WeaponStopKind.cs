namespace XRim.Simulation.Drivers
{
    /// <summary>Why the rules stopped a weapon on its path.</summary>
    public enum WeaponStopKind
    {
        /// <summary>It landed its last allowed hit (D26): it recoils back along its path.</summary>
        LastHit = 0,

        /// <summary>Its attack was cancelled (§9 interrupt, or its dummy died): it is held where it was.</summary>
        Interrupted = 1,
    }
}
