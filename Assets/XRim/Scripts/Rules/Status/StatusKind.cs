namespace XRim.Rules.Status
{
    public enum StatusKind
    {
        /// <summary>From a single head hit above the stun threshold (GDD §11).</summary>
        Stunned = 0,

        /// <summary>From losing a crush-through clash (GDD §10). Proposed to share the stun's definition.</summary>
        Staggered = 1,
    }
}
