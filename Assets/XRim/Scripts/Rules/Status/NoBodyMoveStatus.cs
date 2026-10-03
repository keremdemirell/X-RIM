using XRim.Rules.Planning;

namespace XRim.Rules.Status
{
    /// <summary>The player cannot set a body move (GDD §11 option; D17 default). Weapon, path and Ready work as usual.</summary>
    public sealed class NoBodyMoveStatus : TimedStatusEffect
    {
        public NoBodyMoveStatus(StatusKind kind, int remainingTurns) : base(kind, remainingTurns)
        {
        }

        public override void ApplyToNextTurn(PlanningConstraints constraints) => constraints.ForbidAllBodyMoves();

        protected override IStatusEffect WithRemainingTurns(int remainingTurns) => new NoBodyMoveStatus(Kind, remainingTurns);
    }
}
