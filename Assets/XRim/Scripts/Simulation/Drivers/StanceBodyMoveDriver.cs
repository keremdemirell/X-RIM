using System;
using XRim.Core;
using XRim.Rules.Settings;
using XRim.Simulation.Physics;

namespace XRim.Simulation.Drivers
{
    /// <summary>
    /// Plays any stance swipe from its data (GDD §5). Crouch, lunge, step back (a real step or, with D12's flag, a lean in
    /// place) and jump differ only in their <see cref="BodyMoveStats"/>, so the move set can change for feel without code.
    /// Over <see cref="BodyMoveStats.DurationSeconds"/> the root eases from the turn-start root to the move's full extent:
    /// <list type="bullet">
    /// <item>a step toward or away from the opponent that stays (positions carry over, §3);</item>
    /// <item>a height measured from standing height: below it a low stance the dummy holds (crouch), above it a hop that peaks
    /// halfway and lands back at standing height with the soles tucked up (jump);</item>
    /// <item>a torso lean, positive toward the opponent;</item>
    /// <item>the soles sliding from where they stood to a stride around the new pelvis spot, the front foot ahead.</item>
    /// </list>
    /// The eases start and end at rest, so the torso, pulled to its root by a strength-limited joint, follows without jerks.
    /// </summary>
    public sealed class StanceBodyMoveDriver : IBodyMoveDriver
    {
        private const float HopPeakProgress = 0.5f;

        private BodyMoveStart _start;
        private Vec2 _goalUnits;
        private float _goalRotationDegrees;
        private float _hopPeakYUnits;
        private bool _isHop;
        private float _frontSoleGoalX;
        private float _backSoleGoalX;
        private float _footLiftUnits;
        private double _durationSeconds;
        private SimTime _end;

        public void Begin(BodyMoveStats move, BodyMoveStart start)
        {
            Guard.NotNull(move, nameof(move));
            _start = start;
            float facing = start.Side.FacingSign();
            Vec2 from = start.Root.PositionUnits;
            float leanDegrees;
            if (move.IsLeanInPlace)
            {
                _goalUnits = from;
                _isHop = false;
                leanDegrees = move.LeanInPlaceDegrees;
            }
            else
            {
                float heightUnits = start.StandingHeightUnits + move.DisplacementUnits.Y;
                _isHop = move.DisplacementUnits.Y > 0f;
                _hopPeakYUnits = heightUnits;
                _goalUnits = new Vec2(from.X + facing * move.DisplacementUnits.X, _isHop ? start.StandingHeightUnits : heightUnits);
                leanDegrees = move.LeanDegrees;
            }

            // Leaning toward the opponent turns the torso clockwise for the left dummy (facing +X), counter-clockwise for the right.
            _goalRotationDegrees = start.Root.RotationDegrees - facing * leanDegrees;
            float halfStride = Math.Max(0f, move.StrideUnits) * 0.5f;
            _frontSoleGoalX = _goalUnits.X + facing * halfStride;
            _backSoleGoalX = _goalUnits.X - facing * halfStride;
            _footLiftUnits = _isHop ? Math.Max(0f, move.FootLiftUnits) : 0f;
            _durationSeconds = Math.Max(0f, move.DurationSeconds);
            _end = SimTime.FromSeconds(_durationSeconds);
        }

        public BodyMoveFrame Evaluate(SimTime time)
        {
            float progress = _durationSeconds > 0.0 ? XMath.Clamp01((float)(time.Seconds / _durationSeconds)) : 1f;
            float eased = XMath.SmoothStep01(progress);
            Vec2 from = _start.Root.PositionUnits;
            float x = XMath.Lerp(from.X, _goalUnits.X, eased);
            float y = _isHop ? HopHeight(from.Y, _hopPeakYUnits, _goalUnits.Y, progress) : XMath.Lerp(from.Y, _goalUnits.Y, eased);
            var root = new BodyPose(new Vec2(x, y), XMath.LerpAngleDegrees(_start.Root.RotationDegrees, _goalRotationDegrees, eased));

            float soleY = LegGeometry.FloorYUnits + _footLiftUnits * HopFraction(progress);
            return new BodyMoveFrame(root,
                new Vec2(XMath.Lerp(_start.FrontSoleUnits.X, _frontSoleGoalX, eased), soleY),
                new Vec2(XMath.Lerp(_start.BackSoleUnits.X, _backSoleGoalX, eased), soleY));
        }

        public bool IsComplete(SimTime time) => time >= _end;

        /// <summary>
        /// A hop's height: up from where it started to the peak in the first half, down to where it lands in the second. Each
        /// half is a parabola (constant acceleration, at rest at the peak), so it reads as a real jump.
        /// </summary>
        private static float HopHeight(float fromY, float peakY, float landY, float progress)
        {
            if (progress <= HopPeakProgress)
            {
                float rising = 1f - progress / HopPeakProgress;
                return peakY - (peakY - fromY) * rising * rising;
            }

            float falling = (progress - HopPeakProgress) / (1f - HopPeakProgress);
            return peakY - (peakY - landY) * falling * falling;
        }

        /// <summary>0 at take-off and landing, 1 at the top of the hop (the same parabola).</summary>
        private static float HopFraction(float progress)
        {
            float fromPeak = (progress - HopPeakProgress) / HopPeakProgress;
            return 1f - fromPeak * fromPeak;
        }
    }
}
