using XRim.Core.Gdd;

namespace XRim.Rules.Combat
{
    /// <summary>Whether another contact between the same two weapons in one turn is resolved again.</summary>
    [GddTbd("§10", "Multiple contacts between two weapons in one turn")]
    public interface IRepeatContactPolicy
    {
        bool ShouldResolve(int earlierContactsBetweenPairThisTurn);
    }
}
