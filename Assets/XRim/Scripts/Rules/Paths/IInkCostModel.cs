using XRim.Core.Gdd;
using XRim.Rules.Settings;

namespace XRim.Rules.Paths
{
    /// <summary>
    /// How much ink a path costs (GDD §6). <see cref="LengthInkCostModel"/> charges plain length;
    /// <see cref="RigidityInkCostModel"/> adds the spear's bend penalty cost = distance × (1 + k·Δθ) for weapons
    /// whose rigidity is enabled. Paths must already be resampled (<see cref="PathResampler"/>), so the angles
    /// between segments do not depend on the touch rate.
    /// </summary>
    [GddTbd("§6", "Keep or cut spear rigidity", Proposal = "Keep only if it feels right")]
    public interface IInkCostModel
    {
        InkMeasurement Measure(WeaponPath path, WeaponStats weapon);
    }
}
