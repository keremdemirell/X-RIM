using System;
using System.Collections.Generic;
using XRim.Core;

namespace XRim.Presentation.Cameras
{
    /// <summary>
    /// Camera shake as a sum of fading tremors (GDD pillar 1; presentation only), each started by an impact: it trembles at a set
    /// frequency with a seeded phase, so the same event shakes the same way in a replay, and fades out over its time. Offsets are shares
    /// of the screen height, so a shake looks the same at any zoom. Real (screen) time, so a hitstop freeze still shakes.
    /// </summary>
    public sealed class CameraShake
    {
        /// <summary>The vertical tremble runs a little faster than the horizontal one, so the shake does not move along a line.</summary>
        private const float VerticalFrequencyRatio = 1.31f;

        private const float FullTurnRadians = 2f * (float)Math.PI;

        private readonly List<Tremor> _tremors = new List<Tremor>();

        public bool IsActive => _tremors.Count > 0;

        /// <param name="amplitudeViewFraction">How far it moves at first, as a share of the screen height.</param>
        public void Trigger(float amplitudeViewFraction, float seconds, float frequencyHz, uint seed)
        {
            if (amplitudeViewFraction <= 0f || seconds <= 0f) return;
            var random = new XorShiftRandom(seed == 0u ? 1u : seed);
            float phaseX = random.NextFloat() * FullTurnRadians;
            float phaseY = random.NextFloat() * FullTurnRadians;
            _tremors.Add(new Tremor(amplitudeViewFraction, seconds, Math.Max(0f, frequencyHz), phaseX, phaseY));
        }

        public void Advance(float realDeltaSeconds)
        {
            if (realDeltaSeconds <= 0f) return;
            for (int i = _tremors.Count - 1; i >= 0; i--)
            {
                Tremor tremor = _tremors[i].Aged(realDeltaSeconds);
                if (tremor.IsOver) _tremors.RemoveAt(i);
                else _tremors[i] = tremor;
            }
        }

        /// <summary>Where the camera is pushed now, as shares of the screen height, each axis kept within the strongest allowed shake.</summary>
        public Vec2 Offset(float maxViewFraction)
        {
            float x = 0f, y = 0f;
            foreach (Tremor tremor in _tremors)
            {
                float strength = tremor.Strength;
                float angle = FullTurnRadians * tremor.FrequencyHz * tremor.AgeSeconds;
                x += strength * (float)Math.Sin(angle + tremor.PhaseX);
                y += strength * (float)Math.Sin(angle * VerticalFrequencyRatio + tremor.PhaseY);
            }

            float max = Math.Max(0f, maxViewFraction);
            return new Vec2(XMath.Clamp(x, -max, max), XMath.Clamp(y, -max, max));
        }

        public void Clear() => _tremors.Clear();

        private readonly struct Tremor
        {
            public float Amplitude { get; }
            public float Seconds { get; }
            public float FrequencyHz { get; }
            public float PhaseX { get; }
            public float PhaseY { get; }
            public float AgeSeconds { get; }

            public Tremor(float amplitude, float seconds, float frequencyHz, float phaseX, float phaseY, float ageSeconds = 0f)
            {
                Amplitude = amplitude;
                Seconds = seconds;
                FrequencyHz = frequencyHz;
                PhaseX = phaseX;
                PhaseY = phaseY;
                AgeSeconds = ageSeconds;
            }

            public bool IsOver => AgeSeconds >= Seconds;

            /// <summary>The amplitude now: full at first, fading out (quadratically) to nothing at its end.</summary>
            public float Strength
            {
                get
                {
                    float left = 1f - XMath.Clamp01(AgeSeconds / Seconds);
                    return Amplitude * left * left;
                }
            }

            public Tremor Aged(float seconds) => new Tremor(Amplitude, Seconds, FrequencyHz, PhaseX, PhaseY, AgeSeconds + seconds);
        }
    }
}
