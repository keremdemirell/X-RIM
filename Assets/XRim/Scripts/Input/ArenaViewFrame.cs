using XRim.Core;

namespace XRim.Input
{
    /// <summary>
    /// Which part of the arena the screen shows, before any view flip: the arena point at the screen centre and how many
    /// arena units the full screen height spans. Resolution and aspect ratio do not enter it, so the mapping from pixels to
    /// arena units is the same on every screen (GDD §4: resolution must not change a budget or an outcome). Axis-aligned
    /// orthographic view only; a flip mirrors the arena about x = 0.
    /// </summary>
    public readonly struct ArenaViewFrame
    {
        public Vec2 CenterUnits { get; }
        public float VisibleHeightUnits { get; }

        public ArenaViewFrame(Vec2 centerUnits, float visibleHeightUnits)
        {
            CenterUnits = centerUnits;
            VisibleHeightUnits = Guard.Positive(visibleHeightUnits, nameof(visibleHeightUnits));
        }
    }
}
