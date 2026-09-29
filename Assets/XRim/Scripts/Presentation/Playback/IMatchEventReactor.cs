using XRim.Rules.Events;

namespace XRim.Presentation.Playback
{
    /// <summary>Anything that reacts to a domain event during playback: VFX, audio, camera, feel, UI.</summary>
    public interface IMatchEventReactor
    {
        void OnMatchEvent(MatchEvent matchEvent);
    }
}
