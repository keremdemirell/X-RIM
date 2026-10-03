using System;
using System.Collections.Generic;
using XRim.Core;
using XRim.Rules.Events;
using XRim.Rules.Match;
using XRim.Rules.Settings;
using XRim.Rules.Status;

namespace XRim.Rules.Combat
{
    /// <summary>
    /// The rules for the two held items touching during one turn, engine-free. It shares each side's <see cref="TurnAttack"/>
    /// with <see cref="HitResolver"/>, so what a clash does carries into the hits that follow. The simulation hands over each
    /// contact in time order and applies what comes back (a weapon knocked off its path, a rebound).
    /// <list type="bullet">
    /// <item><b>Clashes</b> (GDD §10): two weapons go to the two-stage <see cref="ClashResolver"/>. A weapon's speed in it is
    /// the speed the rules move it along its path now, 0 when it is not travelling (A1), so at least one weapon must be
    /// travelling for a clash. The crush-through winner continues with less damage on its later hits
    /// (<see cref="TurnAttack.CrushedThrough"/>); the loser is knocked off its path and its dummy staggered (the stun's effect,
    /// D17); a deflected weapon is knocked off without a stagger; a rebound stops both.</item>
    /// <item><b>Blocks</b> (GDD §7): a travelling weapon meeting a shield goes to <see cref="IShieldBlockModel"/>, never the clash
    /// model, wherever the shield is and whether it moves or not. A full block stops the attack; a partial block lets it carry
    /// on with its later hits reduced (<see cref="TurnAttack.ShieldBlockDamageMultiplier"/>, A5); a D21 option can stagger the
    /// holder. A weapon that is not travelling has no attack to block, and two shields meeting have no rule (A7).</item>
    /// <item><b>Repeats</b> (D19): <see cref="IRepeatContactPolicy"/> decides whether a later contact between the pair is resolved
    /// at all; clashes and blocks share the count, and contacts the rules ignore (nothing travelling, a dead wielder) do not
    /// count.</item>
    /// <item>A dead dummy's weapon or shield takes part in no rule.</item>
    /// </list>
    /// </summary>
    public sealed class WeaponContactResolver
    {
        private static readonly Side[] BothSides = { Side.Left, Side.Right };

        private readonly MatchState _state;
        private readonly RulesSettings _settings;
        private readonly RulePolicies _policies;
        private readonly ClashResolver _clash = new ClashResolver();
        private int _resolvedContacts;

        /// <param name="state">The match state being resolved this turn; a stagger adds a status to it.</param>
        /// <param name="attacks">Each side's attack this turn, the same objects the turn's <see cref="HitResolver"/> uses.</param>
        public WeaponContactResolver(MatchState state, RulesSettings settings, RulePolicies policies, PerSide<TurnAttack> attacks)
        {
            _state = Guard.NotNull(state, nameof(state));
            _settings = Guard.NotNull(settings, nameof(settings));
            _policies = Guard.NotNull(policies, nameof(policies));
            Attacks = Guard.NotNull(attacks, nameof(attacks));
            Guard.NotNull(policies.RepeatContact, nameof(RulePolicies.RepeatContact));
            Guard.NotNull(policies.ShieldBlock, nameof(RulePolicies.ShieldBlock));
            Guard.NotNull(policies.StatusEffects, nameof(RulePolicies.StatusEffects));
        }

        public PerSide<TurnAttack> Attacks { get; }

        /// <summary>Resolves one contact between the two weapons (neither a shield), or ignores it.</summary>
        /// <param name="contactAngleDegrees">§10 stage 1: 0° = sliding along each other, 90° = square impact.</param>
        /// <param name="weaponsTravelling">Whether each side's weapon is travelling its drawn path at that instant.</param>
        /// <param name="pathSpeedsUnitsPerSecond">Each weapon's path speed this turn, a body move's bonus included (D13).</param>
        /// <returns>False when the rules ignore the contact: it passes through, physics only.</returns>
        public bool TryResolveClash(SimTime time, float contactAngleDegrees, Vec2 contactPointUnits, PerSide<bool> weaponsTravelling,
            PerSide<float> pathSpeedsUnitsPerSecond, out ClashResolution resolution)
        {
            Guard.NotNull(weaponsTravelling, nameof(weaponsTravelling));
            Guard.NotNull(pathSpeedsUnitsPerSecond, nameof(pathSpeedsUnitsPerSecond));
            resolution = null;
            foreach (Side side in BothSides)
            {
                WeaponStats weapon = Attacks[side].Weapon;
                if (weapon == null) return false;
                if (weapon.Kind == WeaponKind.Shield)
                    throw new InvalidOperationException("A shield contact uses the block model (§7), never the clash model.");
            }

            if (!BothAlive()) return false;
            if (!IsMoving(Side.Left, weaponsTravelling) && !IsMoving(Side.Right, weaponsTravelling)) return false;
            if (!TakeRepeatSlot()) return false;

            PerSide<float> speeds = PerSide<float>.Create(side => SpeedOf(side, weaponsTravelling, pathSpeedsUnitsPerSecond));
            var facts = new ClashFacts(contactAngleDegrees, Participant(Side.Left, speeds), Participant(Side.Right, speeds));
            ClashResult result = _clash.Resolve(facts, _settings.Clash);

            var events = new List<MatchEvent> { new WeaponClashEvent(time, result, contactPointUnits) };
            Apply(result, time, events);
            resolution = new ClashResolution(time, result, events);
            return true;
        }

        /// <summary>Resolves one contact between a travelling weapon and the other side's shield, or ignores it.</summary>
        /// <param name="blocker">The side holding the shield.</param>
        /// <param name="faceAngleDegrees">The weapon's motion relative to the shield against the shield face: 90° = straight into it.</param>
        /// <param name="facePositionFraction">Where along the face it met the shield: 0 = the middle, 1 = the end of the face.</param>
        /// <param name="weaponsTravelling">Whether each side's held item is travelling its drawn path at that instant.</param>
        /// <param name="pathSpeedsUnitsPerSecond">Each held item's path speed this turn, a body move's bonus included (D13).</param>
        /// <returns>False when the rules ignore the contact: it passes through, physics only.</returns>
        public bool TryResolveBlock(SimTime time, Side blocker, float faceAngleDegrees, float facePositionFraction, Vec2 contactPointUnits,
            PerSide<bool> weaponsTravelling, PerSide<float> pathSpeedsUnitsPerSecond, out BlockResolution resolution)
        {
            Guard.NotNull(weaponsTravelling, nameof(weaponsTravelling));
            Guard.NotNull(pathSpeedsUnitsPerSecond, nameof(pathSpeedsUnitsPerSecond));
            resolution = null;
            WeaponStats shield = Attacks[blocker].Weapon;
            if (shield == null || shield.Kind != WeaponKind.Shield)
                throw new InvalidOperationException("Only a side holding a shield blocks (§7).");

            Side attacker = blocker.Opponent();
            WeaponStats weapon = Attacks[attacker].Weapon;
            if (weapon == null || weapon.Kind == WeaponKind.Shield) return false;
            if (!BothAlive() || !IsMoving(attacker, weaponsTravelling) || !TakeRepeatSlot()) return false;

            TurnAttack attack = Attacks[attacker];
            float speed = SpeedOf(attacker, weaponsTravelling, pathSpeedsUnitsPerSecond);
            var facts = new BlockFacts(faceAngleDegrees, facePositionFraction, weapon, speed, shield);
            BlockResult result = _policies.ShieldBlock.Resolve(facts, _settings.Clash.ShieldBlock);

            var events = new List<MatchEvent>
            {
                new ShieldBlockEvent(time, blocker, result, contactPointUnits, faceAngleDegrees, facePositionFraction),
            };
            if (result.AttackStopped) attack.StopWith(AttackStop.Blocked);
            else attack.ReduceByShield(result.DamageMultiplier);
            if (result.ShieldHolderStaggered) Stagger(blocker, time, events);

            resolution = new BlockResolution(time, blocker, result, events);
            return true;
        }

        /// <summary>
        /// The speed the rules move the side's weapon along its path now (A1): its path speed this turn (a body move's bonus
        /// included, D13) times the share it kept after its hits (D26); 0 when it is not travelling or the rules stopped it.
        /// </summary>
        public float SpeedOf(Side side, PerSide<bool> weaponsTravelling, PerSide<float> pathSpeedsUnitsPerSecond)
        {
            Guard.NotNull(weaponsTravelling, nameof(weaponsTravelling));
            Guard.NotNull(pathSpeedsUnitsPerSecond, nameof(pathSpeedsUnitsPerSecond));
            return IsMoving(side, weaponsTravelling) ? pathSpeedsUnitsPerSecond[side] * Attacks[side].SpeedFraction : 0f;
        }

        private bool BothAlive() => !_state.Fighters.Left.IsDead && !_state.Fighters.Right.IsDead;

        /// <summary>The repeat policy (D19) lets this contact between the pair count; if so, it is counted.</summary>
        private bool TakeRepeatSlot()
        {
            if (!_policies.RepeatContact.ShouldResolve(_resolvedContacts, _settings.Clash)) return false;
            _resolvedContacts++;
            return true;
        }

        /// <summary>Travelling its drawn path and not stopped by the rules (a hit, a clash) earlier this turn.</summary>
        private bool IsMoving(Side side, PerSide<bool> weaponsTravelling) => weaponsTravelling[side] && !Attacks[side].IsStopped;

        private ClashParticipant Participant(Side side, PerSide<float> speeds) =>
            new ClashParticipant(side, Attacks[side].Weapon.Mass, speeds[side]);

        private void Apply(ClashResult result, SimTime time, List<MatchEvent> events)
        {
            foreach (Side side in BothSides)
            {
                TurnAttack attack = Attacks[side];
                if (result.Continues(side))
                {
                    if (result.Kind == ClashKind.CrushThrough) attack.CrushedThrough = true;
                    continue;
                }

                attack.StopWith(result.Rebounds ? AttackStop.Rebounded : AttackStop.KnockedOff);
                if (result.Staggers(side)) Stagger(side, time, events);
            }
        }

        /// <summary>A stagger shares the head stun's definition (§10 proposal, D17).</summary>
        private void Stagger(Side side, SimTime time, List<MatchEvent> events)
        {
            _state.Fighters[side].Statuses.Add(_policies.StatusEffects.Create(StatusKind.Staggered, _settings.Damage));
            events.Add(new StatusAppliedEvent(time, side, StatusKind.Staggered));
        }
    }
}
