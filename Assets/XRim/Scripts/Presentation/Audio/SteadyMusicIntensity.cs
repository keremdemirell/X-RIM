using XRim.Core.Gdd;
using XRim.Rules.Match;

namespace XRim.Presentation.Audio
{
    /// <summary>
    /// The default of the adaptive-music seam (GDD §2: tense duel music is Decided, adapting its intensity is TBD; designer 2026-10-11:
    /// a seam only): the music plays steadily whatever the board. Another <see cref="IMusicIntensityDriver"/> could rise at low HP or in
    /// sudden death, the GDD's proposal.
    /// </summary>
    [GddTbd("§2", "Adaptive music", Proposal = "Rise in intensity at low HP and in sudden death")]
    public sealed class SteadyMusicIntensity : IMusicIntensityDriver
    {
        public void OnBoardChanged(MatchState state)
        {
        }
    }
}
