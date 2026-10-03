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
    /// The combat rules at the physics seam (Sessions 06 and 07; ARCHITECTURE §2: Simulation detects, Rules decide, Simulation
    /// applies). Every contact is recorded raw for the debug list. One instant at a time, in time order (GDD §9 priority):
    /// <list type="number">
    /// <item><b>Held item against held item</b> first (A2: at the same instant the blade or shield in the way wins the tie), in
    /// stable order, through the rules' <see cref="WeaponContactResolver"/>:
    /// <list type="bullet">
    /// <item>two weapons clash (§10, the contact's angle and the weapons' powers): a rebound bounces both back, a knocked-off
    /// weapon is kicked away from the winner along the contact normal and flies free before the hand takes it, a survivor
    /// carries on along its path unchanged;</item>
    /// <item>a weapon meeting the other side's shield is blocked (§7), measured from where the shield is at that instant (the
    /// weapon's motion against its face, and where on the face it landed): a full block bounces the weapon back and pushes
    /// the holder back (D21's "physics knockback does the rest"), a partial block lets the weapon carry on with its later hits
    /// reduced;</item>
    /// <item>two shields meeting have no rule (A7).</item>
    /// </list></item>
    /// <item><b>A held item touching another dummy's body part</b> becomes <see cref="HitFacts"/> (the zone from the part's tag,
    /// the weapon from what the attacker holds, the time refined from the weapon's motion) and goes to the rules'
    /// <see cref="HitResolver"/>: the weapon slows (D26) or stops and recoils at its last hit; an interrupted weapon stops where it
    /// is; the struck part gets a share of the weapon's momentum along its motion and the victim is knocked back (E1).</item>
    /// </list>
    /// Both resolvers share each side's <see cref="TurnAttack"/>, so a clash or block carries into the hits after it (a crushed
    /// weapon lands nothing, a crush-through or partially blocked weapon hits softer). A dummy's contacts with its own body,
    /// body-to-body bumps and severed limbs on the floor change nothing.
    /// </summary>
    public sealed class HitContactHandler : ITurnContactHandler
    {
        /// <summary>Below this relative speed the contact has no direction to measure, so the push goes straight back.</summary>
        private const float MinMotionUnitsPerSecond = 1e-3f;

        private static readonly Side[] BothSides = { Side.Left, Side.Right };

        private readonly List<HitFacts> _hits = new List<HitFacts>();
        private readonly List<TurnContact> _hitContacts = new List<TurnContact>();
        private readonly List<TurnContact> _itemContacts = new List<TurnContact>();
        private HitResolver _hitResolver;
        private WeaponContactResolver _itemResolver;

        public void BeginTurn(TurnContactContext context)
        {
            Guard.NotNull(context, nameof(context));
            ITurnActions actions = context.Actions;
            PerSide<TurnAttack> attacks = PerSide<TurnAttack>.Create(side => new TurnAttack(actions.HeldWeapon(side), context.BodyMoves[side]));
            _hitResolver = new HitResolver(context.State, context.Rules, context.Policies, attacks);
            _itemResolver = new WeaponContactResolver(context.State, context.Rules, context.Policies, attacks);
        }

        public void Handle(IReadOnlyList<TurnContact> contacts, TurnContactContext context)
        {
            Guard.NotNull(contacts, nameof(contacts));
            Guard.NotNull(context, nameof(context));
            if (_hitResolver == null) throw new InvalidOperationException("BeginTurn must be called before contacts are handled.");

            int index = 0;
            while (index < contacts.Count)
            {
                SimTime time = contacts[index].Time;
                _hits.Clear();
                _hitContacts.Clear();
                _itemContacts.Clear();
                for (; index < contacts.Count && contacts[index].Time == time; index++)
                {
                    TurnContact contact = contacts[index];
                    context.Record(new ContactEvent(contact));
                    if (IsBetweenHeldItems(contact.Facts))
                    {
                        _itemContacts.Add(contact);
                    }
                    else if (TryReadHit(contact, context.State, out HitFacts hit))
                    {
                        _hits.Add(hit);
                        _hitContacts.Add(contact);
                    }
                }

                foreach (TurnContact contact in _itemContacts)
                {
                    ResolveItemContact(contact, context);
                }

                if (_hits.Count == 0) continue;
                ApplyHits(_hitResolver.Resolve(_hits, Travelling(context.Actions, time)), context);
            }

            context.FirstValidHitTime.Left = _hitResolver.FirstHitTime.Left;
            context.FirstValidHitTime.Right = _hitResolver.FirstHitTime.Right;
        }

        private static PerSide<bool> Travelling(ITurnActions actions, SimTime time) =>
            PerSide<bool>.Create(side => actions.IsWeaponTravelling(side, time));

        // --- Held item against held item: clashes and blocks (§7, §10) ---------------------------------

        private static bool IsBetweenHeldItems(ContactFacts facts) =>
            facts.A.Role == BodyRole.HeldItem && facts.B.Role == BodyRole.HeldItem && facts.A.Owner.HasValue && facts.B.Owner.HasValue &&
            facts.A.Owner.Value != facts.B.Owner.Value;

        private void ResolveItemContact(TurnContact contact, TurnContactContext context)
        {
            ITurnActions actions = context.Actions;
            WeaponStats left = actions.HeldWeapon(Side.Left);
            WeaponStats right = actions.HeldWeapon(Side.Right);
            if (left == null || right == null) return;

            bool leftShield = left.Kind == WeaponKind.Shield;
            bool rightShield = right.Kind == WeaponKind.Shield;
            if (leftShield && rightShield) return;

            PerSide<bool> travelling = Travelling(actions, contact.Time);
            PerSide<float> pathSpeeds = PerSide<float>.Create(side => actions.WeaponSpeedUnitsPerSecond(side));
            // The speeds the rules move the weapons at, read before the contact stops either of them.
            PerSide<float> speeds = PerSide<float>.Create(side => _itemResolver.SpeedOf(side, travelling, pathSpeeds));
            Vec2 point = contact.Facts.PointUnits;
            if (leftShield || rightShield)
            {
                Side blocker = leftShield ? Side.Left : Side.Right;
                MeasureBlock(contact, actions.HeldItemPoseAt(blocker, contact.Time), leftShield ? left : right, out float angle, out float facePosition);
                if (_itemResolver.TryResolveBlock(contact.Time, blocker, angle, facePosition, point, travelling, pathSpeeds, out BlockResolution block))
                    ApplyBlock(block, contact, speeds, context);
                return;
            }

            if (_itemResolver.TryResolveClash(contact.Time, contact.ContactAngleDegrees, point, travelling, pathSpeeds, out ClashResolution clash))
                ApplyClash(clash, contact, speeds, context);
        }

        /// <summary>
        /// The shield's face at the contact (A3: it looks along its body's rotation, its height runs across): the angle between the
        /// two items' relative motion and the face (90° = straight in), and where along the face the contact is (0 = the middle,
        /// 1 = its end).
        /// </summary>
        private static void MeasureBlock(TurnContact contact, BodyPose shieldPose, WeaponStats shield, out float angle, out float facePosition)
        {
            HeldItemShape shape = HeldItemShape.Of(shield);
            Vec2 faceNormal = Vec2.FromAngleDegrees(shieldPose.RotationDegrees);
            var across = new Vec2(-faceNormal.Y, faceNormal.X);
            angle = ContactAngle.Degrees(faceNormal, contact.Facts.RelativeVelocityUnitsPerSecond);
            float halfHeight = shape.SizeUnits.Y * 0.5f;
            Vec2 fromCentre = contact.Facts.PointUnits - HeldItemShape.ToArena(shape.CentreLocal, shieldPose);
            facePosition = halfHeight > 0f ? Math.Abs(Vec2.Dot(fromCentre, across)) / halfHeight : 0f;
        }

        /// <param name="speeds">The speeds the rules moved the weapons at just before the contact.</param>
        private static void ApplyBlock(BlockResolution block, TurnContact contact, PerSide<float> speeds, TurnContactContext context)
        {
            if (block.Result.AttackStopped)
            {
                Side attacker = block.Attacker;
                ITurnActions actions = context.Actions;
                actions.StopWeapon(attacker, WeaponStopKind.Rebounded);
                float pathSpeed = actions.WeaponSpeedUnitsPerSecond(attacker);
                float speedShare = pathSpeed > 0f ? speeds[attacker] / pathSpeed : 0f;
                Push(attacker, block.Blocker, BodyPart.Torso, MotionOf(attacker, contact), speedShare,
                    context.Simulation.ClashReaction.BlockKnockbackFraction, context);
            }

            Record(block.Events, context);
        }

        /// <param name="speeds">The speeds the rules moved the weapons at just before the contact.</param>
        private static void ApplyClash(ClashResolution clash, TurnContact contact, PerSide<float> speeds, TurnContactContext context)
        {
            ClashResult result = clash.Result;
            ITurnActions actions = context.Actions;
            foreach (Side side in BothSides)
            {
                if (result.Rebounds) actions.StopWeapon(side, WeaponStopKind.Rebounded);
                else if (result.IsKnockedOff(side))
                    actions.StopWeapon(side, WeaponStopKind.KnockedOff, KnockOffVelocity(side, result.Winner.Value, contact, speeds, context));
            }

            Record(clash.Events, context);
        }

        /// <summary>
        /// The kick a knocked-off weapon gets (A6): away from the winner along the contact normal, at the winner's momentum (its
        /// mass × the speed the rules move it, as in the clash) over the loser's mass, times the knock-off share.
        /// </summary>
        private static Vec2 KnockOffVelocity(Side loser, Side winner, TurnContact contact, PerSide<float> speeds, TurnContactContext context)
        {
            ITurnActions actions = context.Actions;
            float loserMass = actions.HeldWeapon(loser).Mass;
            Vec2 normal = contact.Facts.Normal;
            if (loserMass <= 0f || normal == Vec2.Zero) return Vec2.Zero;

            Vec2 awayFromWinner = contact.Facts.B.Owner == loser ? normal.Normalized : -normal.Normalized;
            float winnerMomentum = actions.HeldWeapon(winner).Mass * speeds[winner];
            return awayFromWinner * (winnerMomentum / loserMass * context.Simulation.ClashReaction.KnockOffMomentumFraction);
        }

        /// <summary>How the side's held item moved relative to the other one: the contact reports B's velocity relative to A.</summary>
        private static Vec2 MotionOf(Side side, TurnContact contact)
        {
            ContactFacts facts = contact.Facts;
            return facts.A.Owner == side ? -facts.RelativeVelocityUnitsPerSecond : facts.RelativeVelocityUnitsPerSecond;
        }

        // --- Held item against a body: hits (§9, §11) -----------------------------------------------

        /// <summary>A held item touching another dummy's body part; nothing else is a hit.</summary>
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

        private void ApplyHits(HitResolution resolution, TurnContactContext context)
        {
            ITurnActions actions = context.Actions;
            int contactIndex = 0;
            foreach (LandedHit landed in resolution.Landed)
            {
                // Landed hits keep the order they were given in, so the matching contact is the next one with that weapon and part.
                while (!SameHit(_hits[contactIndex], landed.Hit)) contactIndex++;
                Side attacker = landed.Hit.Attacker;
                Push(attacker, landed.Hit.Victim, landed.Hit.Part, WeaponMotionIntoVictim(_hitContacts[contactIndex]), landed.SpeedFractionAtHit,
                    1f, context);
                contactIndex++;

                if (landed.StopsWeapon) actions.StopWeapon(attacker, WeaponStopKind.LastHit);
                else actions.SlowWeapon(attacker, landed.SpeedFractionAfter);
            }

            foreach (Side side in resolution.Interrupted)
            {
                actions.StopWeapon(side, WeaponStopKind.Interrupted);
            }

            Record(resolution.Events, context);
        }

        /// <summary>How the weapon moved relative to the struck body: the contact reports B's velocity relative to A.</summary>
        private static Vec2 WeaponMotionIntoVictim(TurnContact contact)
        {
            ContactFacts facts = contact.Facts;
            return facts.A.Role == BodyRole.HeldItem ? -facts.RelativeVelocityUnitsPerSecond : facts.RelativeVelocityUnitsPerSecond;
        }

        private static bool SameHit(HitFacts a, HitFacts b) => a.Attacker == b.Attacker && a.Part == b.Part && a.Time == b.Time;

        // --- Shared ---------------------------------------------------------------------------

        /// <summary>
        /// What the attacker's weapon does to a dummy it hits (or to the holder of a shield that fully blocked it, scaled down): the
        /// part gets a share of the weapon's momentum along its motion, and the dummy is knocked back away from the attacker by a
        /// distance that grows with the weapon's mass (E1). Both shrink with the share of its speed the weapon had (D26).
        /// </summary>
        private static void Push(Side attacker, Side victim, BodyPart part, Vec2 motion, float speedShare, float scale, TurnContactContext context)
        {
            ITurnActions actions = context.Actions;
            WeaponStats weapon = actions.HeldWeapon(attacker);
            if (weapon == null) return;

            HitReactionSettings reaction = context.Simulation.HitReaction;
            var away = new Vec2(attacker.FacingSign(), 0f);
            Vec2 direction = motion.Length >= MinMotionUnitsPerSecond ? motion.Normalized : away;
            float momentum = weapon.Mass * actions.WeaponSpeedUnitsPerSecond(attacker) * speedShare;
            actions.ApplyImpulse(victim, part, direction * (momentum * reaction.PartImpulseMomentumFraction * scale));

            if (!reaction.KnockbackPersists) return;
            float distance = weapon.Mass * reaction.KnockbackUnitsPerWeaponMass * speedShare * scale;
            actions.KnockBack(victim, away * distance);
        }

        private static void Record(IReadOnlyList<MatchEvent> events, TurnContactContext context)
        {
            foreach (MatchEvent matchEvent in events)
            {
                context.Record(matchEvent);
            }
        }
    }
}
