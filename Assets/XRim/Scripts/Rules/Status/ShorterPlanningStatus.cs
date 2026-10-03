using XRim.Core;
using XRim.Rules.Planning;

namespace XRim.Rules.Status
{
    /// <summary>The player's planning phase is shortened (GDD §11 option).</summary>
    public sealed class ShorterPlanningStatus : TimedStatusEffect
    {
        public float DurationFraction { get; }

        public ShorterPlanningStatus(StatusKind kind, int remainingTurns, float durationFraction) : base(kind, remainingTurns)
        {
            DurationFraction = Guard.Positive(durationFraction, nameof(durationFraction));
        }

        public override void ApplyToNextTurn(PlanningConstraints constraints) =>
            constraints.PlanningDurationSeconds *= DurationFraction;

        protected override IStatusEffect WithRemainingTurns(int remainingTurns) =>
            new ShorterPlanningStatus(Kind, remainingTurns, DurationFraction);
    }
}
