using System;
using System.Collections.Generic;
using XRim.Core;
using XRim.Rules;
using XRim.Rules.Combat;
using XRim.Rules.Events;
using XRim.Rules.Match;
using XRim.Rules.Settings;
using XRim.Simulation.Drivers;
using XRim.Simulation.Physics;
using XRim.Simulation.Settings;

namespace XRim.Simulation.Execution
{
    /// <summary>
    /// The hit rules at the physics seam (Session 06; ARCHITECTURE §2: Simulation detects, Rules decide, Simulation applies).
    /// Every contact is recorded raw for the debug list. A weapon touching another dummy's body part becomes
    /// <see cref="HitFacts"/>: the zone from the part's tag, the weapon from what the attacker holds, the time refined from the
    /// weapon's motion. Each instant's hits go to the rules' <see cref="HitResolver"/>, and what it decides is applied:
    /// <list type="bullet">
    /// <item>the attacker's weapon slows (D26), or stops and recoils at its last hit;</item>
    /// <item>an interrupted weapon stops where it is;</item>
    /// <item>the struck part gets a share of the weapon's momentum as an impulse along the weapon's motion, and the victim is
    /// knocked back away from the attacker by a distance that grows with the weapon's mass (E1); both shrink with the speed
    /// the weapon had left (D26);</item>
    /// <item>the hit, stun, interrupt and death events are recorded at their time.</item>
    /// </list>
    /// A dummy's contacts with its own body, body-to-body bumps, severed limbs on the floor and weapon-to-weapon contacts
    /// (clashes, Session 07) deal no damage.
    /// </summary>
    public sealed class HitContactHandler : ITurnContactHandler
    {
        /// <summary>Below this relative speed the contact has no direction to measure, so the knock goes straight back.</summary>
        private const float MinMotionUnitsPerSecond = 1e-3f;

        private readonly List<HitFacts> _hits = new List<HitFacts>();
        private readonly List<TurnContact> _hitContacts = new List<TurnContact>();
        private HitResolver _resolver;

        public void BeginTurn(TurnContactContext context)
        {
            Guard.NotNull(context, nameof(context));
            ITurnActions actions = context.Actions;
            _resolver = new HitResolver(context.State, context.Rules, context.Policies,
                PerSide<TurnAttack>.Create(side => new TurnAttack(actions.HeldWeapon(side), context.BodyMoves[side])));
        }

        public void Handle(IReadOnlyList<TurnContact> contacts, TurnContactContext context)
        {
            Guard.NotNull(contacts, nameof(contacts));
            Guard.NotNull(context, nameof(context));
            if (_resolver == null) throw new InvalidOperationException("BeginTurn must be called before contacts are handled.");

            int index = 0;
            while (index < contacts.Count)
            {
                SimTime time = contacts[index].Time;
                _hits.Clear();
                _hitContacts.Clear();
                for (; index < contacts.Count && contacts[index].Time == time; index++)
                {
                    TurnContact contact = contacts[index];
                    context.Record(new ContactEvent(contact));
                    if (!TryReadHit(contact, context.State, out HitFacts hit)) continue;
                    _hits.Add(hit);
                    _hitContacts.Add(contact);
                }

                if (_hits.Count == 0) continue;
                ITurnActions actions = context.Actions;
                PerSide<bool> travelling = PerSide<bool>.Create(side => actions.IsWeaponTravelling(side, time));
                Apply(_resolver.Resolve(_hits, travelling), context);
            }

            context.FirstValidHitTime.Left = _resolver.FirstHitTime.Left;
            context.FirstValidHitTime.Right = _resolver.FirstHitTime.Right;
        }

        /// <summary>A held weapon touching another dummy's body part; nothing else is a hit.</summary>
        private static bool TryReadHit(TurnContact contact, MatchState state, out HitFacts hit)
        {
            hit = default;
            ContactFacts facts = contact.Facts;
            BodyTag weapon, body;
            if (facts.A.Role == BodyRole.HeldItem && facts.B.Role == BodyRole.BodyPart)
            {
                weapon = facts.A;
                body = facts.B;
            }
            else if (facts.B.Role == BodyRole.HeldItem && facts.A.Role == BodyRole.BodyPart)
            {
                weapon = facts.B;
                body = facts.A;
            }
            else
            {
                return false;
            }

            if (!weapon.Owner.HasValue || !body.Owner.HasValue || weapon.Owner.Value == body.Owner.Value) return false;

            Side attacker = weapon.Owner.Value;
            FighterState fighter = state.Fighters[attacker];
            hit = new HitFacts(attacker, body.Part, fighter.CurrentWeapon, contact.Time, !fighter.HasDominantArm);
            return true;
        }

        private void Apply(HitResolution resolution, TurnContactContext context)
        {
            ITurnActions actions = context.Actions;
            HitReactionSettings reaction = context.Simulation.HitReaction;
            int contactIndex = 0;
            foreach (LandedHit landed in resolution.Landed)
            {
                // Landed hits keep the order they were given in, so the matching contact is the next one with that weapon and part.
                while (!SameHit(_hits[contactIndex], landed.Hit)) contactIndex++;
                Knock(landed, _hitContacts[contactIndex], actions, reaction);
                contactIndex++;

                Side attacker = landed.Hit.Attacker;
                if (landed.StopsWeapon) actions.StopWeapon(attacker, WeaponStopKind.LastHit);
                else actions.SlowWeapon(attacker, landed.SpeedFractionAfter);
            }

            foreach (Side side in resolution.Interrupted)
            {
                actions.StopWeapon(side, WeaponStopKind.Interrupted);
            }

            foreach (MatchEvent matchEvent in resolution.Events)
            {
                context.Record(matchEvent);
            }
        }

        private static void Knock(LandedHit landed, TurnContact contact, ITurnActions actions, HitReactionSettings reaction)
        {
            Side attacker = landed.Hit.Attacker;
            WeaponStats weapon = actions.HeldWeapon(attacker);
            if (weapon == null) return;

            var away = new Vec2(attacker.FacingSign(), 0f);
            Vec2 motion = WeaponMotionIntoVictim(contact);
            Vec2 direction = motion.Length >= MinMotionUnitsPerSecond ? motion.Normalized : away;
            float momentum = weapon.Mass * actions.WeaponSpeedUnitsPerSecond(attacker) * landed.SpeedFractionAtHit;
            actions.ApplyImpulse(landed.Hit.Victim, landed.Hit.Part, direction * (momentum * reaction.PartImpulseMomentumFraction));

            if (!reaction.KnockbackPersists) return;
            float distance = weapon.Mass * reaction.KnockbackUnitsPerWeaponMass * landed.SpeedFractionAtHit;
            actions.KnockBack(landed.Hit.Victim, away * distance);
        }

        /// <summary>How the weapon moved relative to the struck body: the contact reports B's velocity relative to A.</summary>
        private static Vec2 WeaponMotionIntoVictim(TurnContact contact)
        {
            ContactFacts facts = contact.Facts;
            return facts.A.Role == BodyRole.HeldItem ? -facts.RelativeVelocityUnitsPerSecond : facts.RelativeVelocityUnitsPerSecond;
        }

        private static bool SameHit(HitFacts a, HitFacts b) => a.Attacker == b.Attacker && a.Part == b.Part && a.Time == b.Time;
    }
}
