using System.Collections.Generic;
using XRim.Core;
using XRim.Rules.Events;
using XRim.Rules.Match;

namespace XRim.Simulation.Execution
{
    /// <summary>
    /// Debug and tests (the force-double-KO cheat, Session 12): knocks queued dummies out at the very start of the next turn through the real
    /// death path (HP to 0, a <see cref="FighterDiedEvent"/>, <see cref="ITurnActions.Die"/>: the weapon drops, the dummy goes limp, its
    /// torso splits), so the end conditions see a real double KO (§3) and sudden death begins; inside sudden death two knockouts at the same
    /// instant are a tie (§14). Everything else goes to the wrapped handler, which starts its turn after the knockouts. Each queued knockout
    /// is used once.
    /// </summary>
    public sealed class ScriptedKnockOutContactHandler : ITurnContactHandler
    {
        private readonly ITurnContactHandler _inner;
        private readonly List<Side> _queued = new List<Side>();

        public ScriptedKnockOutContactHandler(ITurnContactHandler inner)
        {
            _inner = Guard.NotNull(inner, nameof(inner));
        }

        /// <summary>The dummies knocked out at the start of the next turn.</summary>
        public IReadOnlyList<Side> Queued => _queued;

        public void QueueKnockOut(Side side)
        {
            if (!_queued.Contains(side)) _queued.Add(side);
        }

        public void QueueDoubleKnockOut()
        {
            QueueKnockOut(Side.Left);
            QueueKnockOut(Side.Right);
        }

        public void BeginTurn(TurnContactContext context)
        {
            Guard.NotNull(context, nameof(context));
            foreach (Side side in _queued)
            {
                FighterState fighter = context.State.Fighters[side];
                if (fighter.IsDead) continue;

                fighter.Hp = 0f;
                context.Record(new FighterDiedEvent(context.Actions.Now, side));
                context.Actions.Die(side);
            }

            _queued.Clear();
            _inner.BeginTurn(context);
        }

        public void Handle(IReadOnlyList<TurnContact> contacts, TurnContactContext context) => _inner.Handle(contacts, context);
    }
}
