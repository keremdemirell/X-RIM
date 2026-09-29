using XRim.Core.Gdd;

namespace XRim.Rules.Paths
{
    /// <summary>What happens when a path goes beyond arm length plus weapon length (plus lunge).</summary>
    [GddTbd("§6", "Paths beyond reach: clip or pull the body")]
    public interface IReachPolicy
    {
        ReachResult Apply(WeaponPath path, float reachUnits);
    }
}
