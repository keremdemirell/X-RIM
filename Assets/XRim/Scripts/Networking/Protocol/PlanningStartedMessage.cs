using XRim.Core;
using XRim.Rules.Planning;
using XRim.Simulation.Recording;

namespace XRim.Networking.Protocol
{
    /// <summary>
    /// A planning phase opens (GDD §3), as the server sends it to one player: the frozen board, each side's limits this turn, this player's
    /// own signature options (never the opponent's), each side's public state, and how long planning lasts from now. Times are left as
    /// durations, not clock readings, because the phone and the server keep different clocks; the phone turns them into its own deadline,
    /// early by the measured delay (designer 2026-10-25, Q5).
    /// </summary>
    public sealed class PlanningStartedMessage : NetMessage
    {
        public override MessageKind Kind => MessageKind.PlanningStarted;

        public int TurnIndex { get; }

        /// <summary>Seconds from when the server sent it until planning ends for both (the later of the two sides' deadlines).</summary>
        public double RemainingSeconds { get; }

        /// <summary>Seconds from when the server sent it until this player's own planning ends (shorter when a stun shortens it, D17).</summary>
        public double OwnRemainingSeconds { get; }

        public bool IsSuddenDeath { get; }

        public PerSide<PlanningConstraints> Constraints { get; }

        /// <summary>The frozen board both players plan from (GDD §3), with only what this player may see of the opponent.</summary>
        public BoardSnapshot Board { get; }

        /// <summary>This player's signature options (GDD §8); the opponent's are never sent.</summary>
        public SignatureOffer OwnSignatures { get; }

        /// <summary>What each side shows as planning opens: weapon, Ready, a charge only when shown.</summary>
        public PerSide<PublicPlanningState> PublicStates { get; }

        public PlanningStartedMessage(int turnIndex, double remainingSeconds, double ownRemainingSeconds, bool isSuddenDeath,
            PerSide<PlanningConstraints> constraints, BoardSnapshot board, SignatureOffer ownSignatures, PerSide<PublicPlanningState> publicStates)
        {
            TurnIndex = turnIndex;
            RemainingSeconds = remainingSeconds;
            OwnRemainingSeconds = ownRemainingSeconds;
            IsSuddenDeath = isSuddenDeath;
            Constraints = Guard.NotNull(constraints, nameof(constraints));
            Board = Guard.NotNull(board, nameof(board));
            OwnSignatures = Guard.NotNull(ownSignatures, nameof(ownSignatures));
            PublicStates = Guard.NotNull(publicStates, nameof(publicStates));
        }
    }
}
