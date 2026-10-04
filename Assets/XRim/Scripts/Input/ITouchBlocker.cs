using XRim.Core;

namespace XRim.Input
{
    /// <summary>Says where a touch belongs to something else (a HUD button), so it must not draw or swipe.</summary>
    public interface ITouchBlocker
    {
        bool Blocks(Vec2 screenPixels);
    }
}
