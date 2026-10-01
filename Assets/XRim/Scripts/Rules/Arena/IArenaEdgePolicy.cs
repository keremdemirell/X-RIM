using XRim.Core.Gdd;
using XRim.Rules.Settings;

namespace XRim.Rules.Arena
{
    /// <summary>
    /// What happens at the arena edge when no electric wall is active (GDD §13, TBD). The physics world builds the edges
    /// it describes, and body moves never plan a dummy's root past a solid edge. D22 default:
    /// <see cref="SolidStopArenaEdgePolicy"/>.
    /// </summary>
    [GddTbd("§13", "Arena width and edge behaviour", Proposal = "D22: width 2000 (placeholder), each edge a solid invisible stop")]
    public interface IArenaEdgePolicy
    {
        ArenaEdges EdgesFor(ArenaSettings arena);
    }
}
