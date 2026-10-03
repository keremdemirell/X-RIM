namespace XRim.Rules.Status
{
    /// <summary>The GDD's options for what a stun does to the next turn (§11, TBD). The stagger shares them (§10 proposal).</summary>
    public enum StunEffect
    {
        /// <summary>No body move next turn (D17 default, designer 2026-10-03).</summary>
        NoBodyMove = 0,

        /// <summary>A shorter planning phase next turn.</summary>
        ShorterPlanning = 1,

        /// <summary>A shorter ink budget next turn.</summary>
        LessInk = 2,
    }
}
