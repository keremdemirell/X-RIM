namespace XRim.Rules.Paths
{
    /// <summary>Outcome of applying the reach limit: a possibly clipped path and an optional body pull.</summary>
    public readonly struct ReachResult
    {
        public WeaponPath Path { get; }

        /// <summary>How far the body is pulled forward to follow the path (0 when the policy clips instead).</summary>
        public float BodyPullUnits { get; }

        public ReachResult(WeaponPath path, float bodyPullUnits)
        {
            Path = path;
            BodyPullUnits = bodyPullUnits;
        }
    }
}
