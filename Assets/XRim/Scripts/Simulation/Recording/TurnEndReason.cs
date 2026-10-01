namespace XRim.Simulation.Recording
{
    /// <summary>Why a turn's execution stopped.</summary>
    public enum TurnEndReason
    {
        /// <summary>Both paths and body moves finished and the physics stayed settled (GDD §3, §9).</summary>
        Settled = 0,

        /// <summary>The execution hard cap was reached first (GDD §3, Appendix A: 1.5 s).</summary>
        HardCap = 1,
    }
}
