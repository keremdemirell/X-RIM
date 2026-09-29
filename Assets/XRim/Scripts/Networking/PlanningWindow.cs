using XRim.Core;
using XRim.Rules.Planning;
using XRim.Simulation.Recording;

namespace XRim.Networking
{
    /// <summary>A planning phase as announced by the authority.</summary>
    public sealed class PlanningWindow
    {
        public int TurnIndex { get; }

        /// <summary>Deadline on the authority's clock. Clients show a countdown toward it.</summary>
        public double DeadlineSeconds { get; }

        public PerSide<PlanningConstraints> Constraints { get; }
        public bool IsSuddenDeath { get; }

        /// <summary>The frozen board both players plan from (GDD §3).</summary>
        public BoardSnapshot Board { get; }

        public PlanningWindow(int turnIndex, double deadlineSeconds, PerSide<PlanningConstraints> constraints,
            bool isSuddenDeath, BoardSnapshot board)
        {
            TurnIndex = turnIndex;
            DeadlineSeconds = deadlineSeconds;
            Constraints = Guard.NotNull(constraints, nameof(constraints));
            IsSuddenDeath = isSuddenDeath;
            Board = Guard.NotNull(board, nameof(board));
        }
    }
}
