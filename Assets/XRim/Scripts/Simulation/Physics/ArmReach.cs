using System;
using XRim.Core;

namespace XRim.Simulation.Physics
{
    /// <summary>
    /// Where the weapon arm's hand can be. The aim model places the grip anywhere within arm reach of the shoulder,
    /// but a physical arm only reaches distances between its fully bent and fully straight lengths (a one-piece arm
    /// reaches exactly its length). The hand therefore holds the blade at the point nearest the grip that the arm can
    /// reach, so arm and weapon never fight; with a one-piece arm, arm and blade line up as one piece.
    /// Units are whatever the caller uses (arena or world), as long as they are consistent.
    /// </summary>
    public static class ArmReach
    {
        /// <summary>Shoulder-to-hand distance of a two-segment arm bent by an angle (0 = straight).</summary>
        public static float DistanceAtBend(float upperLength, float lowerLength, float bendDegrees)
        {
            double cos = Math.Cos(bendDegrees * XMath.DegreesToRadians);
            double squared = upperLength * upperLength + lowerLength * lowerLength + 2.0 * upperLength * lowerLength * cos;
            return (float)Math.Sqrt(Math.Max(0.0, squared));
        }

        /// <summary>
        /// How far from the grip, along the blade axis, the hand holds the weapon: 0 when the grip is within reach,
        /// otherwise the point at reach on the blade line. A point on the blade (0 to <paramref name="bladeLength"/>)
        /// is preferred; if the blade line never comes within reach, the point nearest the shoulder.
        /// </summary>
        public static float HandAlongBlade(Vec2 shoulder, Vec2 grip, Vec2 bladeAxis, float bladeLength, float minReach, float maxReach)
        {
            Vec2 offset = grip - shoulder;
            float distance = offset.Length;
            if (distance >= minReach && distance <= maxReach) return 0f;

            float reach = XMath.Clamp(distance, minReach, maxReach);
            float along = Vec2.Dot(offset, bladeAxis);
            float discriminant = along * along - offset.LengthSquared + reach * reach;
            if (discriminant < 0f) return -along;

            float root = (float)Math.Sqrt(discriminant);
            float forward = -along + root;
            float backward = -along - root;
            bool forwardOnBlade = forward >= 0f && forward <= bladeLength;
            bool backwardOnBlade = backward >= 0f && backward <= bladeLength;
            if (forwardOnBlade != backwardOnBlade) return forwardOnBlade ? forward : backward;
            return Math.Abs(forward) <= Math.Abs(backward) ? forward : backward;
        }

        /// <summary>
        /// The elbow of a two-segment arm whose hand is at <paramref name="hand"/>, for a dummy facing +X whose elbow
        /// bends forward (counter-clockwise). A hand out of reach is treated as at the nearest reachable distance.
        /// </summary>
        public static Vec2 Elbow(Vec2 shoulder, Vec2 hand, float upperLength, float lowerLength)
        {
            Vec2 offset = hand - shoulder;
            float reach = XMath.Clamp(offset.Length, Math.Abs(upperLength - lowerLength), upperLength + lowerLength);
            float handAngle = offset == Vec2.Zero ? 0f : offset.AngleDegrees;
            double cos = reach > 0f
                ? (upperLength * upperLength + reach * reach - lowerLength * lowerLength) / (2.0 * upperLength * reach)
                : 1.0;
            float shoulderTurn = (float)Math.Acos(Math.Max(-1.0, Math.Min(1.0, cos))) * XMath.RadiansToDegrees;
            return shoulder + Vec2.FromAngleDegrees(handAngle - shoulderTurn) * upperLength;
        }
    }
}
