using XRim.Core.Gdd;
using XRim.Simulation.Recording;

namespace XRim.Presentation.Moments
{
    /// <summary>
    /// Spots "did you see that?" turns (GDD pillar 1), e.g. a duck under a mace swing with a same-turn rapier thrust,
    /// a sever, or a sudden-death win by a few milliseconds, so they can get a cinematic replay.
    /// </summary>
    [GddTbd("§17", "Replays and clip sharing")]
    public interface IMomentDetector
    {
        bool IsLegendary(TurnResult result, out string reason);
    }
}
