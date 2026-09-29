namespace XRim.Rules.Paths
{
    /// <summary>Everything <see cref="PathBuilder"/> produces for one stroke: what executes, and what the preview shows.</summary>
    public sealed class BuiltPath
    {
        /// <summary>The drawn polyline after the stroke policy, before any rule touched it. Keep it for the next stroke.</summary>
        public WeaponPath Drawn { get; }

        /// <summary>The resampled path with its lead-in, clamped to reach, before the ink and break cuts (for the preview).</summary>
        public WeaponPath Reachable { get; }

        /// <summary>The path that executes, measured against the weapon's ink.</summary>
        public InkedPath Inked { get; }

        public WeaponPath Path => Inked.Path;

        public bool WasClampedByReach { get; }

        /// <summary>True when the path had a too-sharp turn and was cut where that turn completes (designer, 2026-09-29).</summary>
        public bool WasCutAtBreak { get; }

        public BuiltPath(WeaponPath drawn, WeaponPath reachable, InkedPath inked, bool wasClampedByReach, bool wasCutAtBreak)
        {
            Drawn = drawn;
            Reachable = reachable;
            Inked = inked;
            WasClampedByReach = wasClampedByReach;
            WasCutAtBreak = wasCutAtBreak;
        }
    }
}
