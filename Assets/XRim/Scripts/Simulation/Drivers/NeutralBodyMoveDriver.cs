using System;
using XRim.Core;
using XRim.Rules.Settings;
using XRim.Simulation.Physics;

namespace XRim.Simulation.Drivers
{
    /// <summary>
    /// No body move (GDD §5 "None": the dummy stays in place; §3: an idle dummy holds its pose). The root holds the
    /// turn-start root, so a dummy that ended the last turn tilted straightens up, a crouched one stays low (designer,
    /// 2026-10-02: a crouch is a stance), and the soles stay planted where they stood. With the flag
    /// <see cref="BodyMoveStats.StandsUpFromLowStance"/> a dummy below standing height rises back to it instead.
    /// </summary>
    public sealed class NeutralBodyMoveDriver : IBodyMoveDriver
    {
        private BodyMoveStart _start;
        private float _goalYUnits;
        private double _durationSeconds;
        private SimTime _end;

        public void Begin(BodyMoveStats move, BodyMoveStart start)
        {
            _start = start;
            float fromY = start.Root.PositionUnits.Y;
            bool standsUp = move != null && move.StandsUpFromLowStance && fromY < start.StandingHeightUnits;
            _goalYUnits = standsUp ? start.StandingHeightUnits : fromY;
            _durationSeconds = standsUp ? Math.Max(0f, move.DurationSeconds) : 0.0;
            _end = SimTime.FromSeconds(_durationSeconds);
        }

        public BodyMoveFrame Evaluate(SimTime time)
        {
            float progress = _durationSeconds > 0.0 ? XMath.Clamp01((float)(time.Seconds / _durationSeconds)) : 1f;
            Vec2 from = _start.Root.PositionUnits;
            var root = new BodyPose(new Vec2(from.X, XMath.Lerp(from.Y, _goalYUnits, XMath.SmoothStep01(progress))),
                _start.Root.RotationDegrees);
            return new BodyMoveFrame(root, OnFloor(_start.FrontSoleUnits), OnFloor(_start.BackSoleUnits));
        }

        public bool IsComplete(SimTime time) => time >= _end;

        private static Vec2 OnFloor(Vec2 sole) => new Vec2(sole.X, LegGeometry.FloorYUnits);
    }
}
