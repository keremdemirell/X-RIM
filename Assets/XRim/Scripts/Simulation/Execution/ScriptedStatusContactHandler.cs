using System.Collections.Generic;
using XRim.Core;
using XRim.Rules.Events;
using XRim.Rules.Match;
using XRim.Rules.Status;

namespace XRim.Simulation.Execution
{
    /// <summary>
    /// Debug and tests (the force-stun cheat, Session 13): at the very start of the next turn, gives queued dummies a status through the
    /// real path a head hit or a lost crush takes (the match's <see cref="IStatusEffectFactory"/>, a <see cref="StatusAppliedEvent"/>), so it
    /// shapes the following turn's planning exactly as a real stun or stagger does (D17: no body move by default). A dead dummy gets none.
    /// Everything else goes to the wrapped handler, which starts its turn afterwards. Each queued status is used once.
    /// </summary>
    public sealed class ScriptedStatusContactHandler : ITurnContactHandler
    {
        private readonly ITurnContactHandler _inner;
        private readonly List<(Side Side, StatusKind Kind)> _queued = new List<(Side, StatusKind)>();

        public ScriptedStatusContactHandler(ITurnContactHandler inner)
        {
            _inner = Guard.NotNull(inner, nameof(inner));
        }

        /// <summary>The statuses given at the start of the next turn.</summary>
        public IReadOnlyList<(Side Side, StatusKind Kind)> Queued => _queued;

        public void QueueStatus(Side side, StatusKind kind)
        {
            if (!_queued.Contains((side, kind))) _queued.Add((side, kind));
        }

        public void BeginTurn(TurnContactContext context)
        {
            Guard.NotNull(context, nameof(context));
            foreach ((Side side, StatusKind kind) in _queued)
            {
                FighterState fighter = context.State.Fighters[side];
                if (fighter.IsDead) continue;

                fighter.Statuses.Add(context.Policies.StatusEffects.Create(kind, context.Rules.Damage));
                context.Record(new StatusAppliedEvent(context.Actions.Now, side, kind));
            }

            _queued.Clear();
            _inner.BeginTurn(context);
        }

        public void Handle(IReadOnlyList<TurnContact> contacts, TurnContactContext context) => _inner.Handle(contacts, context);
    }
}
