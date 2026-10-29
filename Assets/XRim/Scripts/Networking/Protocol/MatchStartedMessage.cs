using XRim.Core;
using XRim.Rules.Match;
using XRim.Simulation.Recording;

namespace XRim.Networking.Protocol
{
    /// <summary>
    /// Both loadouts revealed (GDD §6, Decided) and the board the match starts on. The server fills in only what this player may see: the
    /// opponent's signature moves stay hidden until used (designer 2026-10-25, Q3).
    /// </summary>
    public sealed class MatchStartedMessage : NetMessage
    {
        public override MessageKind Kind => MessageKind.MatchStarted;

        public MatchSetup Setup { get; }

        public BoardSnapshot InitialBoard { get; }

        public MatchStartedMessage(MatchSetup setup, BoardSnapshot initialBoard)
        {
            Setup = Guard.NotNull(setup, nameof(setup));
            InitialBoard = Guard.NotNull(initialBoard, nameof(initialBoard));
        }
    }
}
