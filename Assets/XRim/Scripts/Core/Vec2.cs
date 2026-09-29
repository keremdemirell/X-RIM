using System;
using System.Globalization;

namespace XRim.Core
{
    /// <summary>
    /// Engine-free 2D vector in arena units. Mutable public fields so Unity can serialize it
    /// inside [Serializable] settings classes.
    /// </summary>
    [Serializable]
    public struct Vec2 : IEquatable<Vec2>
    {
        public float X;
        public float Y;

        public Vec2(float x, float y)
        {
            X = x;
            Y = y;
        }

        public static Vec2 Zero => new Vec2(0f, 0f);
        public static Vec2 UnitX => new Vec2(1f, 0f);
        public static Vec2 UnitY => new Vec2(0f, 1f);

        public float LengthSquared => X * X + Y * Y;
        public float Length => (float)Math.Sqrt(LengthSquared);

        public Vec2 Normalized
        {
            get
            {
                float length = Length;
                return length > 0f ? new Vec2(X / length, Y / length) : Zero;
            }
        }

        public static float Dot(Vec2 a, Vec2 b) => a.X * b.X + a.Y * b.Y;

        /// <summary>Z component of the 3D cross product.</summary>
        public static float Cross(Vec2 a, Vec2 b) => a.X * b.Y - a.Y * b.X;

        public static float Distance(Vec2 a, Vec2 b) => (a - b).Length;

        /// <summary>Unit vector at an angle, counter-clockwise from +X.</summary>
        public static Vec2 FromAngleDegrees(float degrees)
        {
            double radians = degrees * XMath.DegreesToRadians;
            return new Vec2((float)Math.Cos(radians), (float)Math.Sin(radians));
        }

        /// <summary>Angle of this vector, counter-clockwise from +X, in (-180, 180].</summary>
        public float AngleDegrees => (float)Math.Atan2(Y, X) * XMath.RadiansToDegrees;

        /// <summary>This vector rotated counter-clockwise.</summary>
        public Vec2 Rotated(float degrees)
        {
            double radians = degrees * XMath.DegreesToRadians;
            float cos = (float)Math.Cos(radians);
            float sin = (float)Math.Sin(radians);
            return new Vec2(X * cos - Y * sin, X * sin + Y * cos);
        }

        public static Vec2 Lerp(Vec2 a, Vec2 b, float t) => new Vec2(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);

        public static Vec2 operator +(Vec2 a, Vec2 b) => new Vec2(a.X + b.X, a.Y + b.Y);
        public static Vec2 operator -(Vec2 a, Vec2 b) => new Vec2(a.X - b.X, a.Y - b.Y);
        public static Vec2 operator -(Vec2 v) => new Vec2(-v.X, -v.Y);
        public static Vec2 operator *(Vec2 v, float s) => new Vec2(v.X * s, v.Y * s);
        public static Vec2 operator *(float s, Vec2 v) => new Vec2(v.X * s, v.Y * s);
        public static Vec2 operator /(Vec2 v, float s) => new Vec2(v.X / s, v.Y / s);
        public static bool operator ==(Vec2 a, Vec2 b) => a.Equals(b);
        public static bool operator !=(Vec2 a, Vec2 b) => !a.Equals(b);

        public bool Equals(Vec2 other) => X.Equals(other.X) && Y.Equals(other.Y);
        public override bool Equals(object obj) => obj is Vec2 other && Equals(other);
        public override int GetHashCode() => (X.GetHashCode() * 397) ^ Y.GetHashCode();

        public override string ToString() =>
            string.Format(CultureInfo.InvariantCulture, "({0:0.###}, {1:0.###})", X, Y);
    }
}
