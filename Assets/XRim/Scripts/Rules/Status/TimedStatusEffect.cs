using XRim.Core;
using XRim.Rules.Planning;

namespace XRim.Rules.Status
{
    /// <summary>What every built-in status effect shares: its kind and a count of the turns it still shapes.</summary>
    public abstract class TimedStatusEffect : IStatusEffect
    {
        protected TimedStatusEffect(StatusKind kind, int remainingTurns)
        {
            Kind = kind;
            RemainingTurns = Guard.Positive(remainingTurns, nameof(remainingTurns));
        }

        public StatusKind Kind { get; }
        public int RemainingTurns { get; }

        public abstract void ApplyToNextTurn(PlanningConstraints constraints);

        public IStatusEffect WithOneTurnUsed() => RemainingTurns > 1 ? WithRemainingTurns(RemainingTurns - 1) : null;

        protected abstract IStatusEffect WithRemainingTurns(int remainingTurns);
    }
}
