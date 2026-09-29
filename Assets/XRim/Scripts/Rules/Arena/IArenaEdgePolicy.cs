using XRim.Core.Gdd;
using XRim.Rules.Settings;

namespace XRim.Rules.Arena
{
    /// <summary>What happens at the arena edge when no electric wall is active.</summary>
    [GddTbd("§13", "Arena width and edge behaviour")]
    public interface IArenaEdgePolicy
    {
        float ResolveEdge(float fighterXUnits, ArenaSettings arena);
    }
}
