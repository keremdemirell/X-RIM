using XRim.Core;
using XRim.Rules.Match;

namespace XRim.Networking.Protocol
{
    /// <summary>The match is over: who won, why and after how many turns.</summary>
    public sealed class MatchEndedMessage : NetMessage
    {
        public override MessageKind Kind => MessageKind.MatchEnded;

        public MatchOutcome Outcome { get; }

        public MatchEndedMessage(MatchOutcome outcome)
        {
            Outcome = Guard.NotNull(outcome, nameof(outcome));
        }
    }
}
