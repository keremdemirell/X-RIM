using System;
using XRim.Core;

namespace XRim.Rules.Combat
{
    /// <summary>
    /// GDD §10 stage 1 (Decided): the angle at which two things meet, measured between their relative motion and the
    /// contact surface. 0° = sliding along each other, 90° = a square impact. The same for both sides, and independent
    /// of which way the normal or the relative velocity points.
    /// </summary>
    public static class ContactAngle
    {
        /// <summary>Below this relative speed there is no motion to measure, so the contact counts as sliding (0°).</summary>
        private const float MinRelativeSpeedUnitsPerSecond = 1e-4f;

        public static float Degrees(Vec2 contactNormal, Vec2 relativeVelocityUnitsPerSecond)
        {
            Vec2 normal = contactNormal.Normalized;
            float speed = relativeVelocityUnitsPerSecond.Length;
            if (normal == Vec2.Zero || speed < MinRelativeSpeedUnitsPerSecond) return 0f;

            float sine = Math.Abs(Vec2.Dot(relativeVelocityUnitsPerSecond / speed, normal));
            return (float)Math.Asin(XMath.Clamp01(sine)) * XMath.RadiansToDegrees;
        }
    }
}
