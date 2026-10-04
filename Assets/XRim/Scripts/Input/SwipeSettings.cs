using System;
using XRim.Config;

namespace XRim.Input
{
    /// <summary>
    /// What a swipe has to look like, as plain numbers, so the classifier and its tests never touch a ScriptableObject.
    /// Built from <see cref="InputConfig"/> with <see cref="From"/>.
    /// </summary>
    public readonly struct SwipeSettings
    {
        /// <summary>A diagonal tolerance of 45° or more leaves no gap between directions.</summary>
        public const float MaxAxisToleranceDegrees = 45f;

        /// <summary>GDD §5: how long a swipe may take (about 0.3 s, Tunable).</summary>
        public float WindowSeconds { get; }

        /// <summary>How far the finger must travel, as a fraction of the screen height.</summary>
        public float MinDistanceScreenFraction { get; }

        /// <summary>How far off a straight line the flick may be and still count (D14: no diagonals).</summary>
        public float AxisToleranceDegrees { get; }

        public SwipeSettings(float windowSeconds, float minDistanceScreenFraction, float axisToleranceDegrees)
        {
            if (windowSeconds <= 0f) throw new ArgumentOutOfRangeException(nameof(windowSeconds), windowSeconds, "Must be greater than zero.");
            if (minDistanceScreenFraction <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(minDistanceScreenFraction), minDistanceScreenFraction, "Must be greater than zero.");
            }

            if (axisToleranceDegrees < 0f || axisToleranceDegrees > MaxAxisToleranceDegrees)
            {
                throw new ArgumentOutOfRangeException(nameof(axisToleranceDegrees), axisToleranceDegrees,
                    "Must be between 0 and " + MaxAxisToleranceDegrees + ".");
            }

            WindowSeconds = windowSeconds;
            MinDistanceScreenFraction = minDistanceScreenFraction;
            AxisToleranceDegrees = axisToleranceDegrees;
        }

        public static SwipeSettings From(InputConfig config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            return new SwipeSettings(config.SwipeWindowSeconds, config.MinSwipeDistanceScreenFraction, config.SwipeAxisToleranceDegrees);
        }
    }
}
