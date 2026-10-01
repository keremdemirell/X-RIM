using XRim.Core;

namespace XRim.Rules.Arena
{
    /// <summary>Where the arena ends on each side (arena units) and whether a dummy is stopped there (GDD §13).</summary>
    public readonly struct ArenaEdges
    {
        public float LeftXUnits { get; }
        public float RightXUnits { get; }

        /// <summary>True: each edge is an invisible solid stop. False: nothing stops a dummy there.</summary>
        public bool IsSolid { get; }

        public ArenaEdges(float leftXUnits, float rightXUnits, bool isSolid)
        {
            LeftXUnits = leftXUnits;
            RightXUnits = rightXUnits;
            IsSolid = isSolid;
        }

        public float WidthUnits => RightXUnits - LeftXUnits;
        public float CentreXUnits => (LeftXUnits + RightXUnits) * 0.5f;

        /// <summary>
        /// Keeps a position at least <paramref name="marginUnits"/> inside solid edges (an arena narrower than twice the
        /// margin keeps it at the centre). Open edges never move it.
        /// </summary>
        public float ClampX(float xUnits, float marginUnits)
        {
            if (!IsSolid) return xUnits;
            float min = LeftXUnits + marginUnits;
            float max = RightXUnits - marginUnits;
            return min > max ? CentreXUnits : XMath.Clamp(xUnits, min, max);
        }
    }
}
