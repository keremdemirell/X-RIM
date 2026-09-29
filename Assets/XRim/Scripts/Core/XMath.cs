using System;

namespace XRim.Core
{
    /// <summary>Engine-free math helpers (UnityEngine.Mathf is not available in pure assemblies).</summary>
    public static class XMath
    {
        public const float DegreesToRadians = (float)(Math.PI / 180.0);
        public const float RadiansToDegrees = (float)(180.0 / Math.PI);

        public static float Clamp(float value, float min, float max) => value < min ? min : value > max ? max : value;

        public static float Clamp01(float value) => Clamp(value, 0f, 1f);

        public static float Lerp(float a, float b, float t) => a + (b - a) * t;
    }
}
