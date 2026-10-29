using XRim.Core;
using XRim.Rules.Planning;

namespace XRim.Networking.Protocol
{
    /// <summary>
    /// One planning input, exactly as the player made it (GDD §18): a weapon pick, a body move, a raw stroke (never a built path: the
    /// server builds the path itself), clearing the path, a signature move or Ready. It carries no time: the server stamps it with its
    /// own clock when it arrives, so a client cannot fake the lock-out or the Ready time.
    /// </summary>
    public sealed class PlanningCommandMessage : NetMessage
    {
        public override MessageKind Kind => MessageKind.PlanningCommand;

        /// <summary>The client's own count of the commands it sent, so the server's verdict (<see cref="CommandAnswerMessage"/>) names it.</summary>
        public int Sequence { get; }

        /// <summary>The turn the command was made for; a command for another turn is stale and refused.</summary>
        public int TurnIndex { get; }

        public PlanningCommand Command { get; }

        public PlanningCommandMessage(int sequence, int turnIndex, PlanningCommand command)
        {
            Sequence = sequence;
            TurnIndex = turnIndex;
            Command = Guard.NotNull(command, nameof(command));
        }
    }
}
