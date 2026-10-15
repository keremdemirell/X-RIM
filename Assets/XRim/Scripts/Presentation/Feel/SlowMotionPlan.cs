using System;
using System.Collections.Generic;
using XRim.Core;

namespace XRim.Presentation.Feel
{
    /// <summary>
    /// The playback speed over a whole turn, planned before it plays (presentation only, GDD pillar 1): real time everywhere except the
    /// slowed stretches around big moments, where the slowest stretch wins when two overlap. It converts a frame's real time into turn
    /// time from wherever playback stands, exactly where the speed is steady and in small steps where it eases, so a slowed stretch
    /// starts on time whatever the frame rate.
    /// </summary>
    public sealed class SlowMotionPlan
    {
        /// <summary>While easing, real time is turned into turn time this many seconds of the turn at a time.</summary>
        private const double RampStepSeconds = 0.0005;

        /// <summary>No stretch plays slower than this, so turn time always moves.</summary>
        private const float MinSpeed = 0.01f;

        /// <summary>Where the speed is steady it is read this far after the step's start, inside the step (steps are never shorter).</summary>
        private const double SteadyProbeSeconds = 1e-7;

        public static readonly SlowMotionPlan None = new SlowMotionPlan(Array.Empty<SlowMotionWindow>());

        private readonly List<SlowMotionWindow> _windows;

        public SlowMotionPlan(IEnumerable<SlowMotionWindow> windows)
        {
            _windows = new List<SlowMotionWindow>(Guard.NotNull(windows, nameof(windows)));
            _windows.Sort((a, b) => a.Start.CompareTo(b.Start));
        }

        public IReadOnlyList<SlowMotionWindow> Windows => _windows;

        /// <summary>The playback speed at a time of the turn (1 = real time).</summary>
        public float SpeedAt(SimTime time) => SpeedAt(time.Seconds);

        /// <summary>How many seconds of the turn pass in <paramref name="realSeconds"/> of real time, starting at <paramref name="from"/>.</summary>
        public float Advance(SimTime from, float realSeconds)
        {
            if (realSeconds <= 0f) return 0f;
            if (_windows.Count == 0) return realSeconds;

            double now = from.Seconds;
            double realLeft = realSeconds;
            double advanced = 0.0;
            while (realLeft > 0.0)
            {
                double step = StepAt(now);
                double speed = SpeedOver(now, step);
                double realNeeded = step / speed;
                if (realNeeded >= realLeft)
                {
                    advanced += realLeft * speed;
                    break;
                }

                advanced += step;
                now += step;
                realLeft -= realNeeded;
            }

            return (float)advanced;
        }

        /// <summary>How many real seconds it takes to play the turn from <paramref name="from"/> to <paramref name="to"/> at this plan's speed.</summary>
        public float RealSecondsBetween(SimTime from, SimTime to)
        {
            double now = from.Seconds;
            double end = to.Seconds;
            double real = 0.0;
            while (now < end)
            {
                double step = Math.Min(StepAt(now), end - now);
                double speed = SpeedOver(now, step);
                real += step / speed;
                now += step;
            }

            return (float)real;
        }

        /// <summary>The speed over a step starting now: its middle while easing, otherwise the steady speed just after now.</summary>
        private double SpeedOver(double now, double step) => SpeedAt(IsRamping(now) ? now + step * 0.5 : now + SteadyProbeSeconds);

        private float SpeedAt(double seconds)
        {
            float speed = 1f;
            foreach (SlowMotionWindow window in _windows)
            {
                speed = Math.Min(speed, window.SpeedAt(seconds));
            }

            return Math.Max(speed, MinSpeed);
        }

        private bool IsRamping(double seconds)
        {
            foreach (SlowMotionWindow window in _windows)
            {
                if (window.IsRampingAt(seconds)) return true;
            }

            return false;
        }

        /// <summary>
        /// A small step while easing; otherwise up to the next place the speed changes (the speed is steady until then). Always moves
        /// forward, even from exactly on a boundary.
        /// </summary>
        private double StepAt(double seconds)
        {
            if (IsRamping(seconds)) return RampStepSeconds;

            double next = double.PositiveInfinity;
            foreach (SlowMotionWindow window in _windows)
            {
                Consider(window.EaseInFromSeconds, seconds, ref next);
                Consider(window.Start.Seconds, seconds, ref next);
                Consider(window.End.Seconds, seconds, ref next);
                Consider(window.EaseOutUntilSeconds, seconds, ref next);
            }

            double step = next - seconds;
            return double.IsInfinity(step) ? double.MaxValue : Math.Max(step, RampStepSeconds * 0.01);
        }

        private static void Consider(double boundary, double now, ref double next)
        {
            if (boundary > now && boundary < next) next = boundary;
        }
    }
}
