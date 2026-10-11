using System;
using System.Collections.Generic;
using XRim.Core;
using XRim.Rules;
using XRim.Rules.Events;
using XRim.Rules.Limbs;
using XRim.Rules.Match;

namespace XRim.Simulation.Execution
{
    /// <summary>
    /// Debug and tests (the force-sever cheat): severs queued limbs at the very start of the next turn through the real sever path
    /// (<see cref="SeverRules"/>, then <see cref="SeverApplier"/>), so the limb falls, the weapon drops and the events play exactly as
    /// after a third hit. The limb's damage is set to its durability. Everything else goes to the wrapped handler, which starts its
    /// turn after the severs, so a dummy that dropped its weapon lands nothing. Each queued sever is used once.
    /// </summary>
    public sealed class ScriptedSeverContactHandler : ITurnContactHandler
    {
        private readonly ITurnContactHandler _inner;
        private readonly List<SeveredLimbRef> _queued = new List<SeveredLimbRef>();
        private readonly SeverRules _severRules = new SeverRules();
        private readonly LimbRules _limbs = new LimbRules();

        public ScriptedSeverContactHandler(ITurnContactHandler inner)
        {
            _inner = Guard.NotNull(inner, nameof(inner));
        }

        /// <summary>The severs waiting for the next turn, in the order they were queued.</summary>
        public IReadOnlyList<SeveredLimbRef> Queued => _queued;

        /// <summary>Severs this limb at the start of the next turn (an arm or a leg).</summary>
        public void QueueSever(Side side, BodyPart part)
        {
            var limb = new SeveredLimbRef(side, part);
            if (!_queued.Contains(limb)) _queued.Add(limb);
        }

        public void BeginTurn(TurnContactContext context)
        {
            Guard.NotNull(context, nameof(context));
            foreach (SeveredLimbRef limb in _queued)
            {
                FighterState fighter = context.State.Fighters[limb.Owner];
                if (fighter.IsSevered(limb.Part)) continue;

                float durability = _limbs.DurabilityOf(limb.Part, context.Rules.Damage);
                fighter.SetLimbDamage(limb.Part, Math.Max(fighter.GetLimbDamage(limb.Part), durability));
                bool holdsItem = context.Actions.HeldWeapon(limb.Owner) != null;
                SeverOutcome sever = _severRules.Sever(context.State, limb.Owner, limb.Part, context.Actions.Now, holdsItem,
                    context.Rules.Damage.LimbLoss);
                if (sever == null) continue;

                SeverApplier.Apply(sever, context.Actions);
                foreach (MatchEvent matchEvent in sever.Events)
                {
                    context.Record(matchEvent);
                }
            }

            _queued.Clear();
            _inner.BeginTurn(context);
        }

        public void Handle(IReadOnlyList<TurnContact> contacts, TurnContactContext context) => _inner.Handle(contacts, context);
    }
}
