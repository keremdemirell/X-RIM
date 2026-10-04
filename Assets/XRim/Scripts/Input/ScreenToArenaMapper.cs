using XRim.Core;
using XRim.Core.Gdd;
using XRim.Simulation.Drivers;
using XRim.Simulation.Physics;

namespace XRim.Input
{
    /// <summary>
    /// Converts screen pixels into arena units in the fighter's torso frame (+X toward the opponent), undoing the
    /// view flip. The scale comes from what the screen shows (<see cref="ArenaViewFrame"/>), not from the pixel count, so
    /// screen size, resolution and touch rate never change any budget or outcome (GDD §4, Decided).
    /// </summary>
    [GddTbd("§18", "Arena unit scale and reference resolution")]
    public sealed class ScreenToArenaMapper
    {
        /// <summary>Screen pixels → arena units in the canonical (unflipped) arena.</summary>
        public Vec2 ToArena(Vec2 screenPixels, ScreenLayout layout, ArenaViewFrame view)
        {
            Guard.NotNull(layout, nameof(layout));
            float unitsPerPixel = view.VisibleHeightUnits / layout.ScreenHeightPixels;
            float shownX = view.CenterUnits.X + (screenPixels.X - layout.ScreenWidthPixels * 0.5f) * unitsPerPixel;
            float shownY = view.CenterUnits.Y + (screenPixels.Y - layout.ScreenHeightPixels * 0.5f) * unitsPerPixel;

            // A flipped view shows the arena mirrored about x = 0 (the arena root's X scale is -1): undo it.
            return new Vec2(layout.ArenaViewFlipped ? -shownX : shownX, shownY);
        }

        /// <summary>
        /// Screen pixels → the fighter's torso frame, where paths live (GDD §6, Decided). <paramref name="root"/> is the
        /// side's turn-start root (<see cref="TurnStartRoot"/>), the frame's origin and rotation.
        /// </summary>
        public Vec2 ToFighterFrame(Vec2 screenPixels, ScreenLayout layout, ArenaViewFrame view, BodyPose root, Side side) =>
            TorsoFrame.ToLocal(ToArena(screenPixels, layout, view), root, side);
    }
}
