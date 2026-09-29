using System;
using XRim.Config;
using XRim.Core;
using XRim.Rules;

namespace XRim.Input
{
    /// <summary>Down = crouch, forward = lunge, backward = step back, up = jump (GDD §5). No diagonals (current proposal).</summary>
    public sealed class FourWaySwipeClassifier : ISwipeClassifier
    {
        private InputConfig Config { get; }

        public FourWaySwipeClassifier(InputConfig config)
        {
            Config = Guard.NotNull(config, nameof(config));
        }

        public BodyMove Classify(Vec2 startPixels, Vec2 endPixels, float durationSeconds, ScreenLayout layout)
        {
            // Placeholder: architecture setup only. Gameplay implementation comes later.
            throw new NotImplementedException("FourWaySwipeClassifier.Classify is not implemented yet.");
        }
    }
}
