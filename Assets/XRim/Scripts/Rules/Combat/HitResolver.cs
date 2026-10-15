using System;
using System.Collections.Generic;
using XRim.Core;
using XRim.Rules.Damage;
using XRim.Rules.Events;
using XRim.Rules.Limbs;
using XRim.Rules.Match;
using XRim.Rules.Settings;
using XRim.Rules.Status;

namespace XRim.Rules.Combat
{
    /// <summary>
    /// The hit rules of one turn, engine-free. The simulation hands over every weapon-to-body contact in time order, one
    /// instant at a time, and applies what comes back (weapons slow or stop, the victim is knocked).
    /// <list type="bullet">
    /// <item><b>Priority</b> (GDD §9, Decided): instants are resolved in time order, so the first weapon to reach a valid hitbox
    /// lands first. A landed hit cancels the victim's attack if <see cref="IInterruptPolicy"/> says so (D15: weapon arm or head;
    /// D16: swing armour). A cancelled attack lands nothing afterwards.</item>
    /// <item><b>The same instant:</b> hits at exactly the same time all land, because neither dummy was hit before its own attack.
    /// What they do to each other's attacks (interrupt, death) takes effect after the instant, so two dummies can trade and
    /// even KO each other. Within the instant, hits are taken in the stable order given.</item>
    /// <item><b>Valid hits:</b> only a weapon travelling its drawn path lands hits (E2, flag
    /// <see cref="DamageSettings.RestingWeaponsDealDamage"/>); a dead dummy deals none; nothing hits a dead dummy or a severed
    /// limb.</item>
    /// <item><b>Several hits</b> (D26): a weapon hits each body part at most once per turn and at most
    /// <see cref="DamageSettings.MaxHitsPerWeaponPerTurn"/> times. Each hit keeps only
    /// <see cref="WeaponStats.SpeedKeptAfterHitFraction"/> of its speed, and the next hit's damage follows the speed left. Its
    /// last hit stops it.</item>
    /// <item><b>Damage</b> through <see cref="DamageCalculator"/>; a strong head hit stuns the victim's next turn
    /// (<see cref="IStatusEffectFactory"/>). A killing hit always stops the dead dummy's attack, even through swing armour,
    /// and a killed dummy is not stunned.</item>
    /// <item><b>Severing</b> (GDD §11, §12): a hit that brings a limb to its durability severs it through <see cref="SeverRules"/>. When
    /// the lost arm held the victim's weapon, shield or club, it drops and the victim's attack ends after the instant
    /// (<see cref="AttackStop.WeaponDropped"/>).</item>
    /// </list>
    /// </summary>
    public sealed class HitResolver
    {
        private static readonly Side[] BothSides = { Side.Left, Side.Right };

        private readonly MatchState _state;
        private readonly RulesSettings _settings;
        private readonly RulePolicies _policies;
        private readonly DamageCalculator _calculator;
        private readonly List<IDamageModifier> _modifiers;
        private readonly SeverRules _severRules = new SeverRules();
        private readonly PerSide<SimTime?> _firstHitTime = new PerSide<SimTime?>(null, null);

        /// <param name="state">The match state being resolved this turn; the resolver changes HP, limb damage and statuses.</param>
        public HitResolver(MatchState state, RulesSettings settings, RulePolicies policies, PerSide<TurnAttack> attacks)
        {
            _state = Guard.NotNull(state, nameof(state));
            _settings = Guard.NotNull(settings, nameof(settings));
            _policies = Guard.NotNull(policies, nameof(policies));
            Attacks = Guard.NotNull(attacks, nameof(attacks));
            Guard.NotNull(policies.Interrupt, nameof(RulePolicies.Interrupt));
            Guard.NotNull(policies.DamageModifiers, nameof(RulePolicies.DamageModifiers));
            Guard.NotNull(policies.StatusEffects, nameof(RulePolicies.StatusEffects));
            Guard.NotNull(policies.WeaponTraits, nameof(RulePolicies.WeaponTraits));
            _calculator = new DamageCalculator(Guard.NotNull(policies.InstantKo, nameof(RulePolicies.InstantKo)));

            // The weapon traits (§15) are damage modifiers too, applied after the rules' own.
            _modifiers = new List<IDamageModifier>(policies.DamageModifiers);
            _modifiers.AddRange(policies.WeaponTraits);
        }

        public PerSide<TurnAttack> Attacks { get; }

        /// <summary>When each side landed its first hit this turn, if it did (sudden death, §14).</summary>
        public PerSide<SimTime?> FirstHitTime => _firstHitTime;

        /// <param name="hits">Every weapon-to-body contact of one instant (all at the same time), in stable order.</param>
        /// <param name="weaponsTravelling">Whether each side's weapon is travelling its drawn path at that instant.</param>
        public HitResolution Resolve(IReadOnlyList<HitFacts> hits, PerSide<bool> weaponsTravelling)
        {
            Guard.NotNull(hits, nameof(hits));
            Guard.NotNull(weaponsTravelling, nameof(weaponsTravelling));
            if (hits.Count == 0) throw new ArgumentException("An instant needs at least one hit.", nameof(hits));

            SimTime time = hits[0].Time;
            foreach (HitFacts hit in hits)
            {
                if (hit.Time != time) throw new ArgumentException("All hits of one instant must share its time.", nameof(hits));
            }

            PerSide<bool> deadBefore = PerSide<bool>.Create(side => _state.Fighters[side].IsDead);
            PerSide<float> damageTakenBefore = PerSide<float>.Create(side => _state.Fighters[side].HpDamageTaken);
            var heals = new List<PendingHeal>();
            var cancel = new PerSide<bool>(false, false);
            var died = new PerSide<bool>(false, false);
            var dropped = new PerSide<bool>(false, false);
            var landed = new List<LandedHit>();
            var severed = new List<SeverOutcome>();
            var events = new List<MatchEvent>();

            foreach (HitFacts hit in hits)
            {
                if (!CanLand(hit, deadBefore, weaponsTravelling)) continue;

                LandedHit result = Land(hit, events, damageTakenBefore, heals);
                landed.Add(result);
                Side victim = hit.Victim;
                if (result.KillsVictim) died[victim] = true;
                if (weaponsTravelling[victim] && (result.KillsVictim || Interrupts(hit, result.Damage))) cancel[victim] = true;
                if (!result.Damage.SeversLimb) continue;

                SeverOutcome sever = _severRules.Sever(_state, victim, hit.Part, hit.Time, HoldsItem(victim) && !dropped[victim],
                    _settings.Damage.LimbLoss);
                if (sever == null) continue;
                severed.Add(sever);
                events.AddRange(sever.Events);
                if (sever.DropsHeldItem) dropped[victim] = true;
            }

            // Recoveries land after every hit of the instant, so two dummies trading blows do not depend on the order they were taken in.
            foreach (PendingHeal heal in heals)
            {
                ApplyHeal(heal, time, events);
            }

            foreach (Side side in BothSides)
            {
                if (dropped[side]) Attacks[side].StopWith(AttackStop.WeaponDropped);
            }

            var interrupted = new List<Side>();
            foreach (Side side in BothSides)
            {
                TurnAttack attack = Attacks[side];
                if (!cancel[side] || attack.IsStopped) continue;
                attack.StopWith(died[side] ? AttackStop.WielderDied : AttackStop.Interrupted);
                interrupted.Add(side);
                events.Add(new AttackInterruptedEvent(time, side));
            }

            var dead = new List<Side>();
            foreach (Side side in BothSides)
            {
                if (!died[side]) continue;
                Attacks[side].StopWith(AttackStop.WielderDied);
                dead.Add(side);
                events.Add(new FighterDiedEvent(time, side));
            }

            return new HitResolution(time, landed, interrupted, dead, severed, events);
        }

        private bool CanLand(HitFacts hit, PerSide<bool> deadBefore, PerSide<bool> weaponsTravelling)
        {
            TurnAttack attack = Attacks[hit.Attacker];
            FighterState victim = _state.Fighters[hit.Victim];
            DamageSettings damage = _settings.Damage;
            if (attack.Weapon == null || attack.IsStopped || deadBefore[hit.Attacker]) return false;
            if (!weaponsTravelling[hit.Attacker] && !damage.RestingWeaponsDealDamage) return false;
            if (victim.IsDead || victim.IsSevered(hit.Part)) return false;
            return !attack.HasHit(hit.Part) && attack.HitsLanded < damage.MaxHitsPerWeaponPerTurn;
        }

        private LandedHit Land(HitFacts hit, List<MatchEvent> events, PerSide<float> damageTakenBefore, List<PendingHeal> heals)
        {
            TurnAttack attack = Attacks[hit.Attacker];
            FighterState victim = _state.Fighters[hit.Victim];
            float speedAtHit = attack.SpeedFraction;
            var context = new DamageContext(hit, attack.Weapon, _settings, attack.BodyMove, attack.CrushedThrough, speedAtHit,
                attack.ShieldBlockDamageMultiplier, damageTakenBefore[hit.Attacker]);
            DamageResult damage = _calculator.Calculate(context, _modifiers, victim);

            DamageCalculator.ApplyTo(victim, hit.Part, damage);
            bool kills = victim.IsDead;
            attack.RegisterHit(hit.Part, _settings.Damage.MaxHitsPerWeaponPerTurn);
            if (!_firstHitTime[hit.Attacker].HasValue) _firstHitTime[hit.Attacker] = hit.Time;

            events.Add(new HitLandedEvent(hit.Time, hit, damage));
            if (damage.Stuns && !kills)
            {
                victim.Statuses.Add(_policies.StatusEffects.Create(StatusKind.Stunned, _settings.Damage));
                events.Add(new StatusAppliedEvent(hit.Time, hit.Victim, StatusKind.Stunned));
            }

            NotifyTraits(hit, attack, damage, heals);
            return new LandedHit(hit, damage, speedAtHit, attack.SpeedFraction, attack.IsStopped, kills);
        }

        /// <summary>
        /// A landed hit, for the traits (§15) its weapon carries: they count it on the attacker and may ask for HP back, which is
        /// applied after the instant. Marks the turn as one in which the attacker hit (lifesteal's "in a row").
        /// </summary>
        private void NotifyTraits(HitFacts hit, TurnAttack attack, DamageResult damage, List<PendingHeal> heals)
        {
            FighterState attacker = _state.Fighters[hit.Attacker];
            attacker.HitLandedThisTurn = true;
            foreach (IWeaponTrait trait in _policies.WeaponTraits)
            {
                if (!WeaponTraitRules.Carries(attack.Weapon, trait.Id)) continue;
                float recovered = trait.OnHitLanded(new TraitHitContext(hit, attacker, attack.Weapon, damage, _settings.Traits));
                if (recovered > 0f) heals.Add(new PendingHeal(hit.Attacker, trait.Id, recovered));
            }
        }

        /// <summary>
        /// Gives the HP back, never above the maximum, and not to a dummy that died this instant. In sudden death HP is not what
        /// decides (§14: the first valid hit wins), so nothing is recovered there and the 1 HP stays.
        /// </summary>
        private void ApplyHeal(PendingHeal heal, SimTime time, List<MatchEvent> events)
        {
            FighterState fighter = _state.Fighters[heal.Side];
            if (fighter.IsDead || _state.IsSuddenDeath) return;

            float before = fighter.Hp;
            float after = Math.Min(_settings.Damage.MaxHp, before + heal.HpRecovered);
            if (after <= before) return;
            fighter.Hp = after;
            events.Add(new WeaponTraitHealEvent(time, heal.Side, heal.TraitId, after - before));
        }

        private readonly struct PendingHeal
        {
            public readonly Side Side;
            public readonly string TraitId;
            public readonly float HpRecovered;

            public PendingHeal(Side side, string traitId, float hpRecovered)
            {
                Side = side;
                TraitId = traitId;
                HpRecovered = hpRecovered;
            }
        }

        /// <summary>The side holds its weapon, shield or club at this instant: it had one this turn and has not dropped it.</summary>
        private bool HoldsItem(Side side) => Attacks[side].Weapon != null && Attacks[side].Stop != AttackStop.WeaponDropped;

        private bool Interrupts(HitFacts hit, DamageResult damage)
        {
            FighterState victim = _state.Fighters[hit.Victim];
            BodyPart? weaponArm = victim.HeldItemArm;
            TurnAttack victimAttack = Attacks[hit.Victim];
            if (victimAttack.Weapon == null) return false;

            bool onWeaponArm = weaponArm.HasValue && hit.Part == weaponArm.Value;
            var check = new InterruptCheck(hit, damage, victimAttack.Weapon, onWeaponArm, _settings.Damage);
            return _policies.Interrupt.Interrupts(check);
        }
    }
}
