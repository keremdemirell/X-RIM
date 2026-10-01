using XRim.Core;

namespace XRim.Rules.Planning
{
    /// <summary>
    /// D10 default (the GDD proposal, not decided): a turn is idle unless the plan has a body move, a drawn path or a
    /// signature move. Switching weapon and pressing Ready alone do not count as a move.
    /// </summary>
    public sealed class AnyMoveIdleTurnPolicy : IIdleTurnPolicy
    {
        public bool IsIdle(TurnPlan plan)
        {
            Guard.NotNull(plan, nameof(plan));
            return plan.BodyMove == BodyMove.None && plan.Path.IsEmpty && plan.Signature.IsEmpty;
        }
    }
}
