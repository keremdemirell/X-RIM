using System;
using XRim.Config;

namespace XRim.Input
{
    /// <summary>The dual-zone scheme's own numbers as plain data (the scheme's feel is TBD, §4–5). Built from <see cref="InputConfig"/> with <see cref="From"/>.</summary>
    public readonly struct DualZoneSettings
    {
        /// <summary>A body-zone tap has to be as quick as a swipe.</summary>
        public float TapMaxSeconds { get; }

        /// <summary>A finger that stays this close to where it landed (a share of the screen height) has not moved: it tapped.</summary>
        public float TapMaxTravelScreenFraction { get; }

        /// <summary>A quick tap in the body zone puts the body move back to neutral.</summary>
        public bool TapInBodyZoneClearsMove { get; }

        public DualZoneSettings(float tapMaxSeconds, float tapMaxTravelScreenFraction, bool tapInBodyZoneClearsMove)
        {
            if (tapMaxSeconds <= 0f) throw new ArgumentOutOfRangeException(nameof(tapMaxSeconds), tapMaxSeconds, "Must be greater than zero.");
            if (tapMaxTravelScreenFraction < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(tapMaxTravelScreenFraction), tapMaxTravelScreenFraction, "Must not be negative.");
            }

            TapMaxSeconds = tapMaxSeconds;
            TapMaxTravelScreenFraction = tapMaxTravelScreenFraction;
            TapInBodyZoneClearsMove = tapInBodyZoneClearsMove;
        }

        public static DualZoneSettings From(InputConfig config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            return new DualZoneSettings(config.SwipeWindowSeconds, config.TapMaxTravelScreenFraction, config.TapInBodyZoneClearsMove);
        }
    }
}
