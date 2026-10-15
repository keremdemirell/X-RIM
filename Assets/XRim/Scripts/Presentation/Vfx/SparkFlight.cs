using System;
using XRim.Core;

namespace XRim.Presentation.Vfx
{
    /// <summary>
    /// Where a spark is a given time after it flew off: it shoots out, slows quickly in the air, droops a little under gravity and fades
    /// out over its life. A pure function of time, so playback can run it at any speed, freeze it in a hitstop or play it again the
    /// same way. Arena units, y up.
    /// </summary>
    public readonly struct SparkFlight
    {
        /// <summary>How fast a spark loses its speed in the air (per second).</summary>
        private const float DragPerSecond = 4f;

        /// <summary>Sparks are light: only this share of gravity pulls them down.</summary>
        private const float GravityShare = 0.35f;

        private readonly Vec2 _start;
        private readonly Vec2 _velocity;
        private readonly float _gravity;

        public float LifetimeSeconds { get; }

        public SparkFlight(Vec2 start, Vec2 velocity, float gravityUnitsPerSecondSquared, float lifetimeSeconds)
        {
            _start = start;
            _velocity = velocity;
            _gravity = Math.Max(0f, gravityUnitsPerSecondSquared) * GravityShare;
            LifetimeSeconds = Math.Max(0f, lifetimeSeconds);
        }

        public Vec2 PositionAt(float seconds)
        {
            float t = Math.Max(0f, seconds);
            float carried = (1f - (float)Math.Exp(-DragPerSecond * t)) / DragPerSecond;
            return new Vec2(_start.X + _velocity.X * carried, _start.Y + _velocity.Y * carried - 0.5f * _gravity * t * t);
        }

        public Vec2 VelocityAt(float seconds)
        {
            float t = Math.Max(0f, seconds);
            float kept = (float)Math.Exp(-DragPerSecond * t);
            return new Vec2(_velocity.X * kept, _velocity.Y * kept - _gravity * t);
        }

        /// <summary>1 when it flies off, fading to 0 at the end of its life (and after).</summary>
        public float AlphaAt(float seconds) => LifetimeSeconds > 0f ? 1f - XMath.Clamp01(seconds / LifetimeSeconds) : 0f;

        public bool IsOverAt(float seconds) => seconds >= LifetimeSeconds;
    }
}
