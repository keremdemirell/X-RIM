using XRim.Core;
using XRim.Rules.Combat;

namespace XRim.Rules.Events
{
    /// <summary>A shield met a travelling weapon and its block rule decided what happened (GDD §7). It carries the angle and the point for the gizmos.</summary>
    public sealed class ShieldBlockEvent : MatchEvent
    {
        public Side Blocker { get; }
        public Side Attacker => Blocker.Opponent();
        public BlockResult Result { get; }
        public Vec2 ContactPointUnits { get; }

        /// <summary>The weapon's motion against the shield face: 90° = straight into it.</summary>
        public float ContactAngleDegrees { get; }

        /// <summary>Where along the face it landed: 0 = the middle, 1 = the end.</summary>
        public float FacePositionFraction { get; }

        public ShieldBlockEvent(SimTime time, Side blocker, BlockResult result, Vec2 contactPointUnits, float contactAngleDegrees,
            float facePositionFraction) : base(time)
        {
            Blocker = blocker;
            Result = result;
            ContactPointUnits = contactPointUnits;
            ContactAngleDegrees = contactAngleDegrees;
            FacePositionFraction = facePositionFraction;
        }
    }
}
