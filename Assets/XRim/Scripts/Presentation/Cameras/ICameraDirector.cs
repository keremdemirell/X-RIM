using XRim.Core.Gdd;
using XRim.Presentation.Playback;

namespace XRim.Presentation.Cameras
{
    /// <summary>Frames the duel and reacts to events (shake, punch-in on legendary moments).</summary>
    [GddTbd("§2", "Camera behaviour", Proposal = "Fixed side view")]
    public interface ICameraDirector : IMatchEventReactor
    {
        /// <summary>Flips the arena view so the local player's own dummy appears on their side of the screen (GDD §4).</summary>
        void SetArenaFlipped(bool flipped);
    }
}
