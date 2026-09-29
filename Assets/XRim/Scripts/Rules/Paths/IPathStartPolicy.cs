using XRim.Core;

namespace XRim.Rules.Paths
{
    /// <summary>
    /// Where a stroke starts relative to the weapon's current tip (GDD §6). Decided by the designer on 2026-09-29
    /// (D3): anywhere, with a lead-in from the tip (<see cref="LeadInFromTipPathStartPolicy"/>).
    /// Works on the drawn polyline, before resampling.
    /// </summary>
    public interface IPathStartPolicy
    {
        WeaponPath ResolveStart(WeaponPath drawn, Vec2 weaponTipLocal);
    }
}
