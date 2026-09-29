using XRim.Core;
using XRim.Rules.Match;
using XRim.Simulation.Recording;

namespace XRim.Networking
{
    /// <summary>Both loadouts are revealed to both players at match start (GDD §6, Decided).</summary>
    public sealed class MatchStartInfo
    {
        public MatchSetup Setup { get; }
        public BoardSnapshot InitialBoard { get; }

        public MatchStartInfo(MatchSetup setup, BoardSnapshot initialBoard)
        {
            Setup = Guard.NotNull(setup, nameof(setup));
            InitialBoard = Guard.NotNull(initialBoard, nameof(initialBoard));
        }
    }
}
