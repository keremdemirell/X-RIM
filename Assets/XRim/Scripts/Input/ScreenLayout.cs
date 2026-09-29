using XRim.Core;

namespace XRim.Input
{
    /// <summary>
    /// The local player's screen layout (GDD §4, Decided): their own dummy and the body zone sit on their side of the
    /// screen, left for right-handed players; a mirrored layout (left-handed) moves both to the right and opens the
    /// drawing zone to the left. Arena sides stay canonical; only the view is flipped.
    /// </summary>
    public sealed class ScreenLayout
    {
        public float ScreenWidthPixels { get; }
        public float BodyZoneWidthFraction { get; }
        public bool IsMirrored { get; }

        /// <summary>The arena side this device's player controls.</summary>
        public Side OwnSide { get; }

        public ScreenLayout(float screenWidthPixels, float bodyZoneWidthFraction, bool isMirrored, Side ownSide)
        {
            ScreenWidthPixels = Guard.Positive(screenWidthPixels, nameof(screenWidthPixels));
            BodyZoneWidthFraction = bodyZoneWidthFraction;
            IsMirrored = isMirrored;
            OwnSide = ownSide;
        }

        /// <summary>True when the arena must be drawn flipped so the player's own dummy appears on their side of the screen.</summary>
        public bool ArenaViewFlipped => (OwnSide == Side.Right) != IsMirrored;

        public ScreenZone ZoneAt(Vec2 screenPixels)
        {
            float fraction = screenPixels.X / ScreenWidthPixels;
            float fromOwnEdge = IsMirrored ? 1f - fraction : fraction;
            return fromOwnEdge <= BodyZoneWidthFraction ? ScreenZone.Body : ScreenZone.Weapon;
        }
    }
}
