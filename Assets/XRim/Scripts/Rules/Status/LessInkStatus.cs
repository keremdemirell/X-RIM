using XRim.Core;
using XRim.Rules.Planning;

namespace XRim.Rules.Status
{
    /// <summary>The player's ink budget is shortened (GDD §11 option): every weapon's ink length is scaled.</summary>
    public sealed class LessInkStatus : TimedStatusEffect
    {
        public float InkLengthFraction { get; }

        public LessInkStatus(StatusKind kind, int remainingTurns, float inkLengthFraction) : base(kind, remainingTurns)
        {
            InkLengthFraction = Guard.Positive(inkLengthFraction, nameof(inkLengthFraction));
        }

        public override void ApplyToNextTurn(PlanningConstraints constraints) =>
            constraints.InkLengthMultiplier *= InkLengthFraction;

        protected override IStatusEffect WithRemainingTurns(int remainingTurns) =>
            new LessInkStatus(Kind, remainingTurns, InkLengthFraction);
    }
}
