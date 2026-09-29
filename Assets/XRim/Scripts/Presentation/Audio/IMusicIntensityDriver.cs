using XRim.Core.Gdd;
using XRim.Rules.Match;

namespace XRim.Presentation.Audio
{
    /// <summary>Tense duel music is Decided (§2); adapting its intensity to the board is not.</summary>
    [GddTbd("§2", "Adaptive music", Proposal = "Rise in intensity at low HP and in sudden death")]
    public interface IMusicIntensityDriver
    {
        void OnBoardChanged(MatchState state);
    }
}
