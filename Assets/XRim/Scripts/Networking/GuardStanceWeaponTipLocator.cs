using XRim.Core;
using XRim.Rules;
using XRim.Rules.Planning;
using XRim.Rules.Settings;
using XRim.Simulation;
using XRim.Simulation.Recording;

namespace XRim.Networking
{
    /// <summary>
    /// PLACEHOLDER until Session 04: every weapon rests in the guard stance of Session 02, whatever the board says.
    /// The tip is the shoulder plus (arm length × the guard hand reach + the weapon's length) along the guard angle,
    /// the same point the placeholder ragdoll's rest pose puts it. Once turns carry poses over (stance persistence,
    /// GDD §3), the weapon rests where the last turn left it, and a locator that reads the board's pose replaces this.
    /// </summary>
    public sealed class GuardStanceWeaponTipLocator : IWeaponTipLocator
    {
        private readonly GuardTips _tips;

        public GuardStanceWeaponTipLocator(RulesSettings rules, SimulationSettings simulation)
        {
            _tips = new GuardTips(Guard.NotNull(rules, nameof(rules)), Guard.NotNull(simulation, nameof(simulation)));
        }

        public IWeaponTipSource ForSide(BoardSnapshot board, Side side) => _tips;

        private sealed class GuardTips : IWeaponTipSource
        {
            private readonly RulesSettings _rules;
            private readonly SimulationSettings _simulation;

            public GuardTips(RulesSettings rules, SimulationSettings simulation)
            {
                _rules = rules;
                _simulation = simulation;
            }

            public Vec2 TipLocal(WeaponId weapon)
            {
                WeaponStats stats = _rules.FindWeapon(weapon)
                    ?? throw new System.InvalidOperationException($"The rules settings have no stats for weapon '{weapon}'.");
                PathSettings paths = _rules.Paths;
                var body = _simulation.Ragdoll;
                return paths.ShoulderOffsetUnits + Vec2.FromAngleDegrees(body.GuardAngleDegrees) *
                       (paths.ArmLengthUnits * body.GuardHandReachFraction + stats.LengthUnits);
            }
        }
    }
}
