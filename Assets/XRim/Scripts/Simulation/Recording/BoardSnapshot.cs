using XRim.Core;
using XRim.Rules.Match;
using XRim.Simulation.Physics;

namespace XRim.Simulation.Recording
{
    /// <summary>
    /// The frozen board players plan from (GDD §3): rules state plus physical pose. Together with two plans
    /// and the settings it is everything needed to resolve a turn.
    /// </summary>
    public sealed class BoardSnapshot
    {
        public MatchState State { get; }
        public PoseSnapshot Pose { get; }

        public BoardSnapshot(MatchState state, PoseSnapshot pose)
        {
            State = Guard.NotNull(state, nameof(state));
            Pose = Guard.NotNull(pose, nameof(pose));
        }

        public BoardSnapshot Clone() => new BoardSnapshot(State.Clone(), Pose.Clone());
    }
}
