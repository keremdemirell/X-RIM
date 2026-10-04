using System;
using XRim.Core;
using XRim.Rules;

namespace XRim.Input
{
    /// <summary>
    /// Down = crouch, up = jump, forward = lunge, backward = step back (GDD §5). Forward is toward the opponent, so it
    /// follows the layout: right on the screen normally, left when mirrored. No diagonals (D14, current proposal): a flick
    /// has to point within <see cref="SwipeSettings.AxisToleranceDegrees"/> of one of the four directions, and one pointing
    /// between two of them does nothing rather than risk a wrong move.
    /// </summary>
    public sealed class FourWaySwipeClassifier : ISwipeClassifier
    {
        private readonly SwipeSettings _settings;

        public FourWaySwipeClassifier(SwipeSettings settings)
        {
            _settings = settings;
        }

        public BodyMove? Classify(Vec2 startPixels, Vec2 endPixels, float durationSeconds, ScreenLayout layout)
        {
            Guard.NotNull(layout, nameof(layout));
            if (durationSeconds < 0f || durationSeconds > _settings.WindowSeconds) return null;

            Vec2 delta = endPixels - startPixels;
            if (delta.Length < _settings.MinDistanceScreenFraction * layout.ScreenHeightPixels) return null;

            float across = Math.Abs(delta.X);
            float along = Math.Abs(delta.Y);
            bool horizontal = across >= along;
            float offAxisDegrees = (float)Math.Atan2(horizontal ? along : across, horizontal ? across : along) * XMath.RadiansToDegrees;
            if (offAxisDegrees > _settings.AxisToleranceDegrees) return null;

            if (horizontal) return layout.HorizontalMove(delta.X);

            return delta.Y > 0f ? BodyMove.Jump : BodyMove.Crouch;
        }
    }
}
