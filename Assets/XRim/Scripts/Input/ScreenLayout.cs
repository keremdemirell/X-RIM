using XRim.Core;
using XRim.Rules;

namespace XRim.Input
{
    /// <summary>
    /// The local player's screen layout (GDD §4, Decided): their own dummy and the body zone sit on their side of the
    /// screen, left for right-handed players; a mirrored layout (left-handed) moves both to the right and opens the
    /// drawing zone to the left. Arena sides stay canonical; only the view is flipped.
    /// Screen positions are Unity pixels: the origin is the bottom-left corner and Y grows upward.
    /// </summary>
    public sealed class ScreenLayout
    {
        public float ScreenWidthPixels { get; }
        public float ScreenHeightPixels { get; }
        public float BodyZoneWidthFraction { get; }
        public bool IsMirrored { get; }

        /// <summary>The arena side this device's player controls.</summary>
        public Side OwnSide { get; }

        public ScreenLayout(float screenWidthPixels, float screenHeightPixels, float bodyZoneWidthFraction, bool isMirrored, Side ownSide)
        {
            ScreenWidthPixels = Guard.Positive(screenWidthPixels, nameof(screenWidthPixels));
            ScreenHeightPixels = Guard.Positive(screenHeightPixels, nameof(screenHeightPixels));
            BodyZoneWidthFraction = bodyZoneWidthFraction;
            IsMirrored = isMirrored;
            OwnSide = ownSide;
        }

        /// <summary>True when the arena must be drawn flipped so the player's own dummy appears on their side of the screen.</summary>
        public bool ArenaViewFlipped => (OwnSide == Side.Right) != IsMirrored;

        /// <summary>
        /// +1 when "forward" (toward the opponent) points to the right of the screen, -1 when it points to the left. The own
        /// dummy stands on the player's side of the screen (left unless mirrored) and faces the other one, whichever arena side
        /// it plays, so only mirroring decides it.
        /// </summary>
        public float ForwardScreenSign => IsMirrored ? -1f : 1f;

        /// <summary>
        /// The horizontal body move a sideways input means on this screen: toward the opponent as the player sees it is a lunge,
        /// away is a step back (GDD §5). Swipes and the debug arrow keys both ask here, so they always agree with the view.
        /// </summary>
        /// <param name="screenDx">The input's direction across the screen: positive is to the right. Zero means no direction (null).</param>
        public BodyMove? HorizontalMove(float screenDx)
        {
            if (screenDx == 0f) return null;
            return screenDx * ForwardScreenSign > 0f ? BodyMove.Lunge : BodyMove.StepBack;
        }

        public ScreenZone ZoneAt(Vec2 screenPixels)
        {
            float fraction = screenPixels.X / ScreenWidthPixels;
            float fromOwnEdge = IsMirrored ? 1f - fraction : fraction;
            return fromOwnEdge <= BodyZoneWidthFraction ? ScreenZone.Body : ScreenZone.Weapon;
        }

        /// <summary>The same layout for a window that changed size (an Editor Game view, a device turning).</summary>
        public ScreenLayout WithScreenSize(float screenWidthPixels, float screenHeightPixels) =>
            new ScreenLayout(screenWidthPixels, screenHeightPixels, BodyZoneWidthFraction, IsMirrored, OwnSide);
    }
}
