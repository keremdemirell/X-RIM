using System;
using XRim.Core;

namespace XRim.Presentation.Vfx
{
    /// <summary>
    /// Where one cosmetic debris piece (a bolt, a nut, a drop of hydraulic fluid, GDD §2) is a given time after it flew off: a ballistic
    /// arc that bounces on the floor, losing speed each bounce, until it lies still. A pure function of time, so playback can run it at
    /// any speed, freeze it in a hitstop or scrub it backward, and a replay shows the same spray. Arena units, y up.
    /// </summary>
    public readonly struct DebrisFlight
    {
        /// <summary>After this many bounces the piece lies where it landed.</summary>
        private const int MaxBounces = 4;

        /// <summary>A bounce slower than this (arena units per second) does not leave the floor.</summary>
        private const float RestingBounceSpeedUnitsPerSecond = 20f;

        private readonly Vec2 _start;
        private readonly Vec2 _velocity;
        private readonly float _spinDegreesPerSecond;
        private readonly float _gravity;
        private readonly float _floorY;
        private readonly float _bounceFraction;

        /// <param name="bounceFraction">The share of its speed a piece keeps at each bounce (0 = it stops dead).</param>
        public DebrisFlight(Vec2 start, Vec2 velocity, float spinDegreesPerSecond, float gravityUnitsPerSecondSquared, float floorY,
            float bounceFraction)
        {
            _start = start;
            _velocity = velocity;
            _spinDegreesPerSecond = spinDegreesPerSecond;
            _gravity = Math.Max(0f, gravityUnitsPerSecondSquared);
            _floorY = floorY;
            _bounceFraction = XMath.Clamp01(bounceFraction);
        }

        /// <summary>How long after it flew off the piece first touches the floor (never, without gravity).</summary>
        public float FirstLandingSeconds => _gravity > 0f ? TimeToFloor(Math.Max(_start.Y, _floorY), _velocity.Y) : float.MaxValue;

        /// <summary>The piece's position and rotation this long after it flew off; before that it is at its start.</summary>
        public Vec2 PositionAt(float seconds, out float rotationDegrees)
        {
            float time = Math.Max(0f, seconds);
            Vec2 position = new Vec2(_start.X, Math.Max(_start.Y, _floorY));
            Vec2 velocity = _velocity;
            float flying = 0f;
            for (int bounce = 0; bounce <= MaxBounces; bounce++)
            {
                float landing = TimeToFloor(position.Y, velocity.Y);
                if (time <= landing || _gravity <= 0f)
                {
                    rotationDegrees = _spinDegreesPerSecond * (flying + time);
                    return new Vec2(position.X + velocity.X * time, position.Y + velocity.Y * time - 0.5f * _gravity * time * time);
                }

                position = new Vec2(position.X + velocity.X * landing, _floorY);
                float impactSpeed = velocity.Y - _gravity * landing;
                velocity = new Vec2(velocity.X * _bounceFraction, -impactSpeed * _bounceFraction);
                time -= landing;
                flying += landing;
                if (velocity.Y < RestingBounceSpeedUnitsPerSecond) break;
            }

            rotationDegrees = _spinDegreesPerSecond * flying;
            return position;
        }

        /// <summary>How long a piece at this height and vertical speed takes to reach the floor.</summary>
        private float TimeToFloor(float height, float verticalSpeed)
        {
            if (_gravity <= 0f) return float.MaxValue;
            float drop = Math.Max(0f, height - _floorY);
            return (verticalSpeed + (float)Math.Sqrt(verticalSpeed * verticalSpeed + 2f * _gravity * drop)) / _gravity;
        }
    }
}
