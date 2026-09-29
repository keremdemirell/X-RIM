using XRim.Core.Gdd;

namespace XRim.Rules.Paths
{
    /// <summary>How a new stroke combines with the path already drawn this turn.</summary>
    [GddTbd("§6", "Strokes per turn", Proposal = "One continuous stroke; redrawing replaces it")]
    public interface IStrokePolicy
    {
        WeaponPath Combine(WeaponPath current, WeaponPath newStroke);
    }
}
