using XRim.Core.Gdd;
using XRim.Simulation.Recording;

namespace XRim.Presentation.Moments
{
    /// <summary>
    /// Spots "did you see that?" turns (GDD pillar 1), such as a duck under a mace swing with a same-turn rapier thrust (§1), a sever, or a
    /// sudden-death win by a few milliseconds, so they get an instant cinematic replay. Saving and sharing replays stays TBD (§17,
    /// Session 21); the replay is local and played from the stored turn.
    /// </summary>
    [GddTbd("§17", "Replays and clip sharing", Proposal = "Local instant replay only (designer 2026-10-11)")]
    public interface IMomentDetector
    {
        /// <summary>The turn's most important legendary moment, if it has one.</summary>
        bool TryDetect(TurnResult turn, out LegendaryMoment moment);
    }
}
