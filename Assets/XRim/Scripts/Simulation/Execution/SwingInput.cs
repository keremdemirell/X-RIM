using XRim.Core;
using XRim.Rules.Match;
using XRim.Rules.Paths;
using XRim.Rules.Settings;
using XRim.Simulation.Physics;

namespace XRim.Simulation.Execution
{
    /// <summary>
    /// Everything one swing depends on: the frozen board (pose and rules state), each side's path, and the settings.
    /// Feel-spike input (Session 02); Session 04 grows it into the full <see cref="TurnInput"/> with body moves.
    /// </summary>
    public sealed class SwingInput
    {
        public PoseSnapshot StartPose { get; }
        public MatchState State { get; }

        /// <summary>Torso-frame paths (GDD §6). Null = that side does not attack; its weapon is held where it is.</summary>
        public PerSide<WeaponPath> Paths { get; }

        public RulesSettings Rules { get; }
        public SimulationSettings Simulation { get; }

        public SwingInput(PoseSnapshot startPose, MatchState state, PerSide<WeaponPath> paths, RulesSettings rules,
            SimulationSettings simulation)
        {
            StartPose = Guard.NotNull(startPose, nameof(startPose));
            State = Guard.NotNull(state, nameof(state));
            Paths = Guard.NotNull(paths, nameof(paths));
            Rules = Guard.NotNull(rules, nameof(rules));
            Simulation = Guard.NotNull(simulation, nameof(simulation));
        }
    }
}
