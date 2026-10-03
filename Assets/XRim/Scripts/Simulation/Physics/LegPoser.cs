using System;
using XRim.Core;
using XRim.Simulation.Settings;

namespace XRim.Simulation.Physics
{
    /// <summary>
    /// The hip and knee angles that put a leg's foot on a target point, so a body move keeps the dummy on its feet: knees
    /// bend in a crouch, feet stay on the floor in a stride and tuck up in a hop (GDD §5). The leg hangs from the pelvis.
    /// With ten bodies the knee points toward the opponent and the angles come from the two-segment leg; a one-piece leg
    /// (six bodies) swings until its foot meets the target's height, forward for a target ahead (or the front leg), back for
    /// one behind, or forward anyway when the hip cannot swing back that far. The leg ends in a flat box end, so a tilted
    /// leg rests on its lowest corner: the target is where that corner goes. Angles are clamped to the joint limits, so a
    /// target out of reach is approached as closely as the leg allows.
    /// </summary>
    public static class LegPoser
    {
        /// <summary>Distances below this count as zero (avoids dividing by zero in a fully folded leg).</summary>
        private const float MinReachUnits = 1e-3f;

        /// <summary>Solves for the end's centre, then raises it by the lowest corner's drop; five passes land within a few hundredths of a unit.</summary>
        private const int CornerPasses = 5;

        public static LimbAngles Solve(BodyPose pelvis, Vec2 footTargetUnits, Side side, bool isFrontLeg, RagdollSegmentation segmentation,
            RagdollSettings body)
        {
            Guard.NotNull(body, nameof(body));
            float halfWidth = body.LegWidthUnits * 0.5f;
            Vec2 endTarget = footTargetUnits;
            LimbAngles angles = default;
            for (int pass = 0; pass < CornerPasses; pass++)
            {
                angles = SolveEnd(pelvis, endTarget, side, isFrontLeg, segmentation, body, out float endSegmentDegrees);
                float cornerDrop = halfWidth * Math.Abs((float)Math.Sin(endSegmentDegrees * XMath.DegreesToRadians));
                endTarget = new Vec2(footTargetUnits.X, footTargetUnits.Y + cornerDrop);
            }

            return angles;
        }

        /// <summary>The angles that put the centre of the leg's end on the target; also the last segment's angle from straight down.</summary>
        private static LimbAngles SolveEnd(BodyPose pelvis, Vec2 endTargetUnits, Side side, bool isFrontLeg, RagdollSegmentation segmentation,
            RagdollSettings body, out float endSegmentDegrees)
        {
            // The fighter's frame: +X toward the opponent, angles counter-clockwise for a dummy facing +X.
            float facing = side.FacingSign();
            Vec2 toEnd = endTargetUnits - pelvis.PositionUnits;
            float ahead = facing * toEnd.X;
            float below = -toEnd.Y;
            float torsoDegrees = facing * pelvis.RotationDegrees;
            float distance = (float)Math.Sqrt(ahead * ahead + below * below);

            // The direction to the target, from straight down; positive points toward the opponent.
            float towardEndDegrees = (float)Math.Atan2(ahead, below) * XMath.RadiansToDegrees;

            if (segmentation == RagdollSegmentation.SixBodies)
            {
                float hip = OnePieceHipDegrees(distance, below, ahead, towardEndDegrees, torsoDegrees, isFrontLeg, body);
                endSegmentDegrees = torsoDegrees + hip;
                return new LimbAngles(hip, 0f);
            }

            float thigh = LegGeometry.ThighLengthUnits(body);
            float shin = body.LegLengthUnits - thigh;
            float reach = XMath.Clamp(distance, Math.Max(Math.Abs(thigh - shin), MinReachUnits), thigh + shin);
            float thighDegrees = towardEndDegrees + InteriorAngleDegrees(thigh, reach, shin);
            float shinDegrees = towardEndDegrees - InteriorAngleDegrees(shin, reach, thigh);
            float hipDegrees = ClampHip(thighDegrees - torsoDegrees, body);
            float kneeDegrees = XMath.Clamp(shinDegrees - thighDegrees, body.KneeMinDegrees, body.KneeMaxDegrees);
            endSegmentDegrees = torsoDegrees + hipDegrees + kneeDegrees;
            return new LimbAngles(hipDegrees, kneeDegrees);
        }

        /// <summary>
        /// A straight leg: points at the target when it is out of reach; otherwise swings until its end is at the target's
        /// height, forward for a target ahead (or the front leg), back for one behind (or the back leg), unless the hip cannot
        /// swing back that far: then forward, so the foot never ends up under the floor.
        /// </summary>
        private static float OnePieceHipDegrees(float distance, float below, float ahead, float towardEndDegrees, float torsoDegrees,
            bool isFrontLeg, RagdollSettings body)
        {
            if (distance >= body.LegLengthUnits || below <= 0f) return ClampHip(towardEndDegrees - torsoDegrees, body);

            float swing = (float)Math.Acos(XMath.Clamp(below / body.LegLengthUnits, -1f, 1f)) * XMath.RadiansToDegrees;
            bool forward = ahead > MinReachUnits || (ahead >= -MinReachUnits && isFrontLeg);
            float hip = (forward ? swing : -swing) - torsoDegrees;
            if (!forward && hip < body.HipMinDegrees) hip = swing - torsoDegrees;
            return ClampHip(hip, body);
        }

        /// <summary>The angle at the corner between sides <paramref name="adjacent"/> and <paramref name="reach"/> (law of cosines).</summary>
        private static float InteriorAngleDegrees(float adjacent, float reach, float opposite)
        {
            float cosine = (adjacent * adjacent + reach * reach - opposite * opposite) / (2f * adjacent * reach);
            return (float)Math.Acos(XMath.Clamp(cosine, -1f, 1f)) * XMath.RadiansToDegrees;
        }

        private static float ClampHip(float degrees, RagdollSettings body) => XMath.Clamp(degrees, body.HipMinDegrees, body.HipMaxDegrees);
    }
}
