using XRim.Core;
using XRim.Rules;
using XRim.Rules.Match;
using XRim.Rules.Settings;
using XRim.Simulation.Drivers;

namespace XRim.Simulation.Physics
{
    /// <summary>
    /// The pose a match starts from: both dummies in the guard stance (<see cref="GuardStance"/>), their pelvis one leg
    /// length above the floor, <see cref="ArenaSettings.StartingGapUnits"/> apart and mirrored around x = 0, each holding its
    /// current weapon in its dominant hand (GDD §12). From then on positions carry over (§3).
    /// </summary>
    public static class StartingBoard
    {
        public static PoseSnapshot Create(MatchState state, RulesSettings rules, SimulationSettings simulation, IWeaponAimModel aim)
        {
            Guard.NotNull(state, nameof(state));
            Guard.NotNull(rules, nameof(rules));
            Guard.NotNull(simulation, nameof(simulation));
            float half = rules.Arena.StartingGapUnits * 0.5f;
            return new PoseSnapshot
            {
                Left = Fighter(Side.Left, -half, state, rules, simulation, aim),
                Right = Fighter(Side.Right, half, state, rules, simulation, aim),
            };
        }

        private static FighterPose Fighter(Side side, float xUnits, MatchState state, RulesSettings rules, SimulationSettings simulation,
            IWeaponAimModel aim)
        {
            FighterState fighter = state.Fighters[side];
            var pelvis = new BodyPose(new Vec2(xUnits, simulation.Ragdoll.LegLengthUnits), TurnStartRoot.UprightDegrees);
            return GuardStance.Create(pelvis, side, BodyParts.DominantArm(fighter.Handedness), rules.FindWeapon(fighter.CurrentWeapon),
                simulation.Segmentation, simulation.Ragdoll, rules.Paths, aim);
        }
    }
}
