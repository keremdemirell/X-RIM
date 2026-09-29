using System;

namespace XRim.Core
{
    /// <summary>Argument checks for engine-free code (UnityEngine.Debug.Assert is not available here).</summary>
    public static class Guard
    {
        public static T NotNull<T>(T value, string paramName) where T : class =>
            value ?? throw new ArgumentNullException(paramName);

        public static float Positive(float value, string paramName) =>
            value > 0f ? value : throw new ArgumentOutOfRangeException(paramName, value, "Must be greater than zero.");

        public static int Positive(int value, string paramName) =>
            value > 0 ? value : throw new ArgumentOutOfRangeException(paramName, value, "Must be greater than zero.");
    }
}
