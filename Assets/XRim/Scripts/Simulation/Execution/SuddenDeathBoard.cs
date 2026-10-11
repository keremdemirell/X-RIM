using XRim.Core;
using XRim.Rules.Match;
using XRim.Rules.Settings;
using XRim.Rules.SuddenDeath;
using XRim.Simulation.Drivers;
using XRim.Simulation.Physics;

namespace XRim.Simulation.Execution
{
    /// <summary>
    /// The pose half of sudden death's setup (GDD §14; the board mode P5, designer 2026-10-11), next to the rules half in
    /// <see cref="SuddenDeathSetup"/>, which the state has already been through:
    /// <list type="bullet">
    /// <item>carry over (the default): the pose the last turn ended in, limbs on the floor included (still clubs); a split torso is rebuilt
    /// whole when the next turn loads (S5, <see cref="TurnBoard"/>);</item>
    /// <item>reset: the match's starting board, both dummies whole on their start lines holding the weapon the state now has, no floor
    /// limbs;</item>
    /// <item>either way each electric wall is drawn as the rules state has it (down unless walls stay live in sudden death).</item>
    /// </list>
    /// </summary>
    public static class SuddenDeathBoard
    {
        /// <param name="ended">The pose the turn that triggered sudden death ended in.</param>
        /// <param name="state">The rules state after <see cref="SuddenDeathSetup.Apply"/>.</param>
        /// <param name="startPose">The pose the match started from, if it was given; null builds the <see cref="StartingBoard"/>.</param>
        public static PoseSnapshot Prepare(PoseSnapshot ended, MatchState state, RulesSettings rules, SimulationSettings simulation,
            IWeaponAimModel aim, PoseSnapshot startPose = null)
        {
            Guard.NotNull(ended, nameof(ended));
            Guard.NotNull(state, nameof(state));
            Guard.NotNull(rules, nameof(rules));
            PoseSnapshot pose = rules.SuddenDeath.BoardMode != SuddenDeathBoardMode.ResetToMatchStart ? ended.Clone()
                : startPose != null ? startPose.Clone()
                : StartingBoard.Create(state, rules, simulation, aim);
            foreach (Side side in new[] { Side.Left, Side.Right })
            {
                pose.SetWall(side, ElectricWallPose.Of(state.Walls[side]));
            }

            return pose;
        }
    }
}
