using System;

namespace XRim.Core
{
    /// <summary>Engine-free math helpers (UnityEngine.Mathf is not available in pure assemblies).</summary>
    public static class XMath
    {
        public const float DegreesToRadians = (float)(Math.PI / 180.0);
        public const float RadiansToDegrees = (float)(180.0 / Math.PI);
        public const float TwoPi = (float)(2.0 * Math.PI);

        private const float FullTurnDegrees = 360f;
        private const float HalfTurnDegrees = 180f;

        public static float Clamp(float value, float min, float max) => value < min ? min : value > max ? max : value;

        public static float Clamp01(float value) => Clamp(value, 0f, 1f);

        public static float Lerp(float a, float b, float t) => a + (b - a) * t;

        /// <summary>Eases 0 → 1 with zero slope at both ends (3t² − 2t³); t is clamped to [0, 1].</summary>
        public static float SmoothStep01(float t)
        {
            float clamped = Clamp01(t);
            return clamped * clamped * (3f - 2f * clamped);
        }

        /// <summary>The shortest signed turn from one angle to another, in [-180, 180).</summary>
        public static float DeltaAngleDegrees(float fromDegrees, float toDegrees)
        {
            float delta = (toDegrees - fromDegrees) % FullTurnDegrees;
            if (delta >= HalfTurnDegrees) delta -= FullTurnDegrees;
            else if (delta < -HalfTurnDegrees) delta += FullTurnDegrees;
            return delta;
        }

        /// <summary>Interpolates between two angles along the shortest turn.</summary>
        public static float LerpAngleDegrees(float fromDegrees, float toDegrees, float t) =>
            fromDegrees + DeltaAngleDegrees(fromDegrees, toDegrees) * t;
    }
}
