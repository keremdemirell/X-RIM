namespace XRim.Rules.Paths
{
    /// <summary>
    /// What happens when a path goes beyond the reach limit (GDD §6). Decided by the designer on 2026-09-29 (D4):
    /// clip at the reach limit by clamping (<see cref="ClampToReachPolicy"/>). The interface stays so the rule
    /// lives in one swappable place.
    /// </summary>
    public interface IReachPolicy
    {
        /// <param name="sampledPath">A path already resampled by <see cref="PathResampler"/>.</param>
        /// <param name="limit">The weapon's reach in the torso frame.</param>
        /// <param name="sampleSpacingUnits">Spacing to restore after points move.</param>
        ReachResult Apply(WeaponPath sampledPath, ReachLimit limit, float sampleSpacingUnits);
    }
}
