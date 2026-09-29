namespace XRim.Rules.Paths
{
    /// <summary>Outcome of applying the reach limit to a sampled path.</summary>
    public readonly struct ReachResult
    {
        public WeaponPath Path { get; }

        /// <summary>True when part of the drawn path lay beyond reach and was pulled onto the reach limit.</summary>
        public bool WasClamped { get; }

        public ReachResult(WeaponPath path, bool wasClamped)
        {
            Path = path;
            WasClamped = wasClamped;
        }
    }
}
