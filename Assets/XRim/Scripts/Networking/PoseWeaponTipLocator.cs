using System;
using XRim.Core;
using XRim.Rules;
using XRim.Rules.Planning;
using XRim.Rules.Settings;
using XRim.Simulation;
using XRim.Simulation.Drivers;
using XRim.Simulation.Physics;
using XRim.Simulation.Recording;

namespace XRim.Networking
{
    /// <summary>
    /// Where each weapon tip rests on the frozen board (GDD §3), the point a stroke's lead-in starts from (D3): read from the
    /// pose the last turn left the weapon in, in the upright turn-start frame the turn will run in (<see cref="TurnStartRoot"/>).
    /// A weapon switched to during planning appears in the same grip, so its tip is the grip plus its own length. A side
    /// holding nothing (its weapon was dropped) plans from the guard stance tip.
    /// </summary>
    public sealed class PoseWeaponTipLocator : IWeaponTipLocator
    {
        private readonly RulesSettings _rules;
        private readonly SimulationSettings _simulation;

        public PoseWeaponTipLocator(RulesSettings rules, SimulationSettings simulation)
        {
            _rules = Guard.NotNull(rules, nameof(rules));
            _simulation = Guard.NotNull(simulation, nameof(simulation));
        }

        public IWeaponTipSource ForSide(BoardSnapshot board, Side side) =>
            new PoseTips(Guard.NotNull(board, nameof(board)).Pose.Get(side).Clone(), side, _rules, _simulation);

        private sealed class PoseTips : IWeaponTipSource
        {
            private readonly FighterPose _pose;
            private readonly Side _side;
            private readonly RulesSettings _rules;
            private readonly SimulationSettings _simulation;

            public PoseTips(FighterPose pose, Side side, RulesSettings rules, SimulationSettings simulation)
            {
                _pose = pose;
                _side = side;
                _rules = rules;
                _simulation = simulation;
            }

            public Vec2 TipLocal(WeaponId weapon)
            {
                WeaponStats stats = _rules.FindWeapon(weapon)
                    ?? throw new InvalidOperationException($"The rules settings have no stats for weapon '{weapon}'.");
                if (!_pose.HasHeldItem) return GuardStance.TipLocal(stats, _simulation.Ragdoll, _rules.Paths);

                BodyPose grip = _pose.HeldItem;
                Vec2 tip = grip.PositionUnits + Vec2.FromAngleDegrees(grip.RotationDegrees) * stats.LengthUnits;
                return TorsoFrame.ToLocal(tip, TurnStartRoot.Of(_pose), _side);
            }
        }
    }
}
