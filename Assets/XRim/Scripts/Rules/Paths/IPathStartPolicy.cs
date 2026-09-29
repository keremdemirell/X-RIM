using XRim.Core;
using XRim.Core.Gdd;

namespace XRim.Rules.Paths
{
    /// <summary>Must the stroke start at the weapon's current tip, or anywhere (the arm moving there first)?</summary>
    [GddTbd("§6", "Path start: at the weapon tip or anywhere")]
    public interface IPathStartPolicy
    {
        WeaponPath ResolveStart(WeaponPath drawn, Vec2 weaponTipLocal);
    }
}
