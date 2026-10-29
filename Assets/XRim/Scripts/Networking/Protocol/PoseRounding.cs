using System;
using System.Collections.Generic;
using XRim.Core;
using XRim.Simulation.Physics;

namespace XRim.Networking.Protocol
{
    /// <summary>
    /// How finely a turn's recorded poses travel (technical, not gameplay). The frames of a <see cref="Simulation.Recording.TurnTimeline"/>
    /// are only played back, so they are rounded to a fine grid, which lets each value be sent as a small difference from what its motion
    /// predicts. The final board, which the next turn is planned and simulated from, travels exactly. Both steps are powers of two, so the
    /// rounded values are exact floats and rounding twice changes nothing.
    /// <list type="bullet">
    /// <item>Positions: 1/16 arena unit, about 0.16 mm at 2.5 mm a unit: a tenth of a pixel at the duel framing on a 1080-px phone
    /// (1.5 px a unit) and 0.15 px in the replay's closer view (2.4 px a unit, ART_SPEC).</item>
    /// <item>Rotations: 1/128 degree, so even the 400-unit rapier's tip moves at most 0.03 units.</item>
    /// </list>
    /// </summary>
    public static class PoseRounding
    {
        public const float PositionStepUnits = 1f / PositionStepsPerUnit;
        public const float RotationStepDegrees = 1f / RotationStepsPerDegree;

        private const float PositionStepsPerUnit = 16f;
        private const float RotationStepsPerDegree = 128f;

        /// <summary>Beyond this many steps a value no longer fits the grid exactly (2^52); no arena value comes near it.</summary>
        private const double MaxSteps = 4503599627370496.0;

        /// <summary>The pose as a client receives it in a timeline frame: every position and rotation on the grid, everything else unchanged.</summary>
        public static PoseSnapshot Round(PoseSnapshot pose)
        {
            PoseSnapshot rounded = pose.Clone();
            RoundFighter(rounded.Left);
            RoundFighter(rounded.Right);
            var limbs = new List<SeveredLimbPose>(rounded.SeveredLimbs.Count);
            foreach (SeveredLimbPose limb in rounded.SeveredLimbs)
            {
                SeveredLimbPose copy = limb;
                copy.Pose = Round(limb.Pose);
                copy.Lower = Round(limb.Lower);
                limbs.Add(copy);
            }

            rounded.SeveredLimbs = limbs;
            rounded.LeftWall.XUnits = Position(PositionSteps(rounded.LeftWall.XUnits));
            rounded.RightWall.XUnits = Position(PositionSteps(rounded.RightWall.XUnits));
            return rounded;
        }

        public static BodyPose Round(BodyPose pose) => new BodyPose(
            new Vec2(Position(PositionSteps(pose.PositionUnits.X)), Position(PositionSteps(pose.PositionUnits.Y))),
            Rotation(RotationSteps(pose.RotationDegrees)));

        internal static long PositionSteps(float units) => Steps(units, PositionStepsPerUnit, "position");

        internal static float Position(long steps) => (float)(steps / (double)PositionStepsPerUnit);

        internal static long RotationSteps(float degrees) => Steps(degrees, RotationStepsPerDegree, "rotation");

        internal static float Rotation(long steps) => (float)(steps / (double)RotationStepsPerDegree);

        private static void RoundFighter(FighterPose fighter)
        {
            for (int i = 0; i < fighter.Parts.Length; i++)
            {
                fighter.Parts[i] = Round(fighter.Parts[i]);
            }

            for (int i = 0; i < fighter.LowerSegments.Length; i++)
            {
                fighter.LowerSegments[i] = Round(fighter.LowerSegments[i]);
            }

            fighter.HeldItem = Round(fighter.HeldItem);
            fighter.DroppedItem = Round(fighter.DroppedItem);
            fighter.TorsoTop = Round(fighter.TorsoTop);
        }

        private static long Steps(float value, float stepsPerUnit, string what)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                throw new ArgumentException($"A recorded pose holds a {what} that is not a number ({value}).", nameof(value));
            double steps = Math.Round(value * (double)stepsPerUnit, MidpointRounding.AwayFromZero);
            if (Math.Abs(steps) > MaxSteps) throw new ArgumentException($"A recorded pose holds a {what} too large to send ({value}).", nameof(value));
            return (long)steps;
        }
    }
}
