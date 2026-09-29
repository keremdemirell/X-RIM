using XRim.Core;

namespace XRim.Rules.Paths
{
    /// <summary>GDD §6, D5 (Decided 2026-09-29): one continuous stroke per turn; a new stroke replaces the old one.</summary>
    public sealed class ReplaceStrokePolicy : IStrokePolicy
    {
        public WeaponPath Combine(WeaponPath current, WeaponPath newStroke) => Guard.NotNull(newStroke, nameof(newStroke));
    }
}
