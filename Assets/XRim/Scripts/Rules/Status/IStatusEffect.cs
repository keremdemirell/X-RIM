using XRim.Core.Gdd;
using XRim.Rules.Planning;

namespace XRim.Rules.Status
{
    /// <summary>
    /// A temporary effect that changes the affected player's next planning phase. Every GDD option
    /// (no body move, shorter planning, less ink) is expressible through <see cref="PlanningConstraints"/>.
    /// </summary>
    [GddTbd("§11", "Effect of a stun", Proposal = "No body move, shorter planning, or less ink")]
    [GddTbd("§10", "Effect of a stagger", Proposal = "Same as head stun")]
    public interface IStatusEffect
    {
        StatusKind Kind { get; }
        int RemainingTurns { get; }

        void ApplyToNextTurn(PlanningConstraints constraints);
    }
}
