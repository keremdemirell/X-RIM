using System;
using System.Collections.Generic;
using XRim.Core;
using XRim.Simulation.Physics;

namespace XRim.Simulation.Execution
{
    /// <summary>
    /// Finds when, inside a physics step, a weapon first touched a contact point (GDD §9: time-to-impact to the
    /// millisecond). The physics engine only reports that a contact exists after a step; this sweeps the blade between
    /// the recorded poses of the last steps and returns the earliest time it was within touching distance of the point.
    /// The caller turns that time into path distance with d = v·t.
    /// A window of two or more steps is needed because Box2D reports a new contact one step after the motion that made it.
    /// </summary>
    public static class ImpactTimeRefiner
    {
        /// <summary>Coarse samples per recorded step before narrowing down; fine enough for a blade turning quickly.</summary>
        private const int SamplesPerStep = 8;

        /// <summary>Halvings of the first touching interval: far below a microsecond at any step rate we use.</summary>
        private const int BisectionIterations = 20;

        /// <param name="contactPointUnits">Where the contact was reported, arena units.</param>
        /// <param name="blade">The weapon's shape.</param>
        /// <param name="touchDistanceUnits">Extra gap at which the physics engine already counts a touch (its contact offset).</param>
        /// <param name="window">The blade's recorded poses in time order, oldest first.</param>
        public static SimTime Refine(Vec2 contactPointUnits, BladeShape blade, float touchDistanceUnits, IReadOnlyList<PoseSample> window)
        {
            Guard.NotNull(window, nameof(window));
            if (window.Count == 0) throw new ArgumentException("The window needs at least one pose.", nameof(window));

            float reach = blade.HalfWidthUnits + touchDistanceUnits;
            if (Gap(contactPointUnits, blade, window[0].Pose, reach) <= 0f) return window[0].Time;

            SimTime closestTime = window[0].Time;
            float closestGap = float.MaxValue;
            for (int i = 1; i < window.Count; i++)
            {
                PoseSample from = window[i - 1];
                PoseSample to = window[i];
                float previousFraction = 0f;
                for (int sample = 1; sample <= SamplesPerStep; sample++)
                {
                    float fraction = (float)sample / SamplesPerStep;
                    float gap = Gap(contactPointUnits, blade, Interpolate(from.Pose, to.Pose, fraction), reach);
                    if (gap <= 0f)
                    {
                        float touch = Bisect(contactPointUnits, blade, reach, from.Pose, to.Pose, previousFraction, fraction);
                        return TimeAt(from.Time, to.Time, touch);
                    }

                    if (gap < closestGap)
                    {
                        closestGap = gap;
                        closestTime = TimeAt(from.Time, to.Time, fraction);
                    }

                    previousFraction = fraction;
                }
            }

            // The blade never came within reach of the point (for example the point lies on the far side of a thick
            // body): the moment of closest approach is the best estimate.
            return closestTime;
        }

        private static float Bisect(Vec2 point, BladeShape blade, float reach, BodyPose from, BodyPose to, float apart, float touching)
        {
            for (int i = 0; i < BisectionIterations; i++)
            {
                float middle = (apart + touching) * 0.5f;
                if (Gap(point, blade, Interpolate(from, to, middle), reach) <= 0f) touching = middle;
                else apart = middle;
            }

            return touching;
        }

        private static float Gap(Vec2 point, BladeShape blade, BodyPose pose, float reach) => blade.DistanceToCentreLine(point, pose) - reach;

        private static BodyPose Interpolate(BodyPose from, BodyPose to, float fraction) =>
            new BodyPose(Vec2.Lerp(from.PositionUnits, to.PositionUnits, fraction),
                XMath.LerpAngleDegrees(from.RotationDegrees, to.RotationDegrees, fraction));

        private static SimTime TimeAt(SimTime from, SimTime to, float fraction) =>
            new SimTime(from.Microseconds + (long)Math.Round((to.Microseconds - from.Microseconds) * (double)fraction));
    }
}
