using XRim.Core.Gdd;
using XRim.Rules.Planning;

namespace XRim.Rules.Status
{
    /// <summary>
    /// A temporary effect that changes the affected player's next planning phase. Every GDD option
    /// (no body move, shorter planning, less ink) is expressible through <see cref="PlanningConstraints"/>.
    /// Effects are immutable: a cloned match state shares them safely, and <see cref="WithOneTurnUsed"/> counts them down.
    /// </summary>
    [GddTbd("§11", "Effect of a stun", Proposal = "No body move, shorter planning, or less ink")]
    [GddTbd("§10", "Effect of a stagger", Proposal = "Same as head stun")]
    public interface IStatusEffect
    {
        StatusKind Kind { get; }

        /// <summary>How many more turns this effect shapes, this one included. At least 1.</summary>
        int RemainingTurns { get; }

        void ApplyToNextTurn(PlanningConstraints constraints);

        /// <summary>This effect after it has shaped one turn: one turn fewer, or null once it has run out.</summary>
        IStatusEffect WithOneTurnUsed();
    }
}
