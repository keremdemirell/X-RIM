using System;
using XRim.Core;
using XRim.Simulation.Physics;

namespace XRim.Presentation.Vfx
{
    /// <summary>
    /// The struck dummy's drawn flinch (GDD pillar 1 and 4; presentation only): its drawing rocks away from the blow about its pelvis and
    /// is pushed back a little, then springs back, harder the more damage the hit dealt. The physics dummy underneath never moves, so
    /// outcomes cannot change; the flinch only exaggerates what the eye sees.
    /// </summary>
    public static class HitFlinch
    {
        /// <summary>The flinch peaks this share of the way through its time, then eases back.</summary>
        private const float PeakShare = 0.15f;

        /// <summary>The flinch's strength a time after the hit (0..1): a fast kick out, then an ease back to nothing by its end.</summary>
        public static float StrengthAt(float secondsSince, float durationSeconds)
        {
            if (durationSeconds <= 0f || secondsSince < 0f || secondsSince >= durationSeconds) return 0f;
            float x = secondsSince / durationSeconds;
            if (x < PeakShare) return x / PeakShare;
            float back = (1f - x) / (1f - PeakShare);
            return back * back;
        }

        /// <summary>How far the victim's drawing turns at full strength: away from the attacker (its top moves away), degrees.</summary>
        public static float AngleDegrees(Side victim, float damage, float degreesPerDamage, float maxDegrees) =>
            victim.FacingSign() * Math.Min(Math.Max(0f, maxDegrees), Math.Max(0f, damage) * degreesPerDamage);

        /// <summary>How far the victim's drawing is pushed back at full strength (away from the attacker), arena units along X.</summary>
        public static float PushUnits(Side victim, float damage, float unitsPerDamage, float maxUnits) =>
            -victim.FacingSign() * Math.Min(Math.Max(0f, maxUnits), Math.Max(0f, damage) * unitsPerDamage);

        /// <summary>A body pose turned about a pivot and shifted: where a part of a flinching dummy is drawn.</summary>
        public static BodyPose Apply(BodyPose pose, Vec2 pivotUnits, float degrees, Vec2 offsetUnits)
        {
            if (degrees == 0f && offsetUnits == Vec2.Zero) return pose;
            Vec2 turned = pivotUnits + (pose.PositionUnits - pivotUnits).Rotated(degrees) + offsetUnits;
            return new BodyPose(turned, pose.RotationDegrees + degrees);
        }
    }
}
