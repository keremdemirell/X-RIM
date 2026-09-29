using XRim.Core.Gdd;
using XRim.Rules.Settings;

namespace XRim.Rules.Paths
{
    /// <summary>
    /// How much ink a path costs. Plain length for most weapons; the spear's bend penalty
    /// cost = distance × (1 + k·Δθ) is one candidate implementation.
    /// </summary>
    [GddTbd("§6", "Keep or cut spear rigidity", Proposal = "Keep only if it feels right")]
    public interface IInkCostModel
    {
        InkMeasurement Measure(WeaponPath path, WeaponStats weapon);
    }
}
