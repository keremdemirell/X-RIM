using XRim.Rules.Planning;

namespace XRim.Networking.Protocol
{
    /// <summary>
    /// The server's verdict on one of this player's commands, sent to that player only. The phone already judged the command with the same
    /// rules for instant feedback; this is the truth, on the server's clock (GDD §18).
    /// </summary>
    public sealed class CommandAnswerMessage : NetMessage
    {
        public override MessageKind Kind => MessageKind.CommandAnswer;

        /// <summary>The <see cref="PlanningCommandMessage.Sequence"/> it answers.</summary>
        public int Sequence { get; }

        /// <summary><see cref="CommandRejection.None"/> when the server accepted it.</summary>
        public CommandRejection Rejection { get; }

        public bool Accepted => Rejection == CommandRejection.None;

        public CommandAnswerMessage(int sequence, CommandRejection rejection)
        {
            Sequence = sequence;
            Rejection = rejection;
        }
    }
}
