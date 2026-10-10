using System;

namespace XRim.Core
{
    /// <summary>
    /// How far each edge of a screen is covered by something that is not the game (a notch, a camera cut-out, rounded corners, a
    /// system bar), in pixels or in any unit scaled from pixels. Engine-free so the arithmetic is testable (GDD §4: the HUD must stay
    /// out of the safe area on a phone). Never negative.
    /// </summary>
    public readonly struct ScreenInsets : IEquatable<ScreenInsets>
    {
        public ScreenInsets(float left, float top, float right, float bottom)
        {
            Left = Math.Max(left, 0f);
            Top = Math.Max(top, 0f);
            Right = Math.Max(right, 0f);
            Bottom = Math.Max(bottom, 0f);
        }

        public float Left { get; }
        public float Top { get; }
        public float Right { get; }
        public float Bottom { get; }

        public static ScreenInsets None => new ScreenInsets(0f, 0f, 0f, 0f);

        public bool IsNone => Left <= 0f && Top <= 0f && Right <= 0f && Bottom <= 0f;

        /// <summary>
        /// The insets for a safe area given the way Unity's <c>Screen.safeArea</c> gives it: a rectangle in screen pixels, origin at the
        /// bottom-left, y up. An empty or impossible rectangle (a screen not set up yet, a safe area bigger than the screen) means no
        /// insets, never a HUD squeezed to nothing.
        /// </summary>
        public static ScreenInsets FromSafeArea(float safeX, float safeY, float safeWidth, float safeHeight, float screenWidth, float screenHeight)
        {
            if (screenWidth <= 0f || screenHeight <= 0f || safeWidth <= 0f || safeHeight <= 0f) return None;

            var insets = new ScreenInsets(safeX, screenHeight - (safeY + safeHeight), screenWidth - (safeX + safeWidth), safeY);
            bool leavesNothing = insets.Left + insets.Right >= screenWidth || insets.Top + insets.Bottom >= screenHeight;
            return leavesNothing ? None : insets;
        }

        /// <summary>Every inset times a factor, for example pixels to the units of a UI that scales with the screen.</summary>
        public ScreenInsets Scaled(float factor) => new ScreenInsets(Left * factor, Top * factor, Right * factor, Bottom * factor);

        public bool Equals(ScreenInsets other) => Left == other.Left && Top == other.Top && Right == other.Right && Bottom == other.Bottom;

        public override bool Equals(object obj) => obj is ScreenInsets other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Left, Top, Right, Bottom);

        public override string ToString() => $"L{Left:0.#} T{Top:0.#} R{Right:0.#} B{Bottom:0.#}";
    }
}
