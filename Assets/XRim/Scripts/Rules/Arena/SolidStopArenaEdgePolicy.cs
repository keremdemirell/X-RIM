using XRim.Core;
using XRim.Rules.Settings;

namespace XRim.Rules.Arena
{
    /// <summary>
    /// D22 (designer, 2026-10-01; GDD §13 stays TBD): the arena is <see cref="ArenaSettings.WidthUnits"/> wide, centred on
    /// x = 0 where the dummies start mirrored, and each edge is a solid invisible stop. Nobody gets a ring-out before the
    /// electric wall exists; a dummy knocked into the edge just thuds against it.
    /// </summary>
    public sealed class SolidStopArenaEdgePolicy : IArenaEdgePolicy
    {
        public ArenaEdges EdgesFor(ArenaSettings arena)
        {
            float half = Guard.NotNull(arena, nameof(arena)).WidthUnits * 0.5f;
            return new ArenaEdges(-half, half, isSolid: true);
        }
    }
}
