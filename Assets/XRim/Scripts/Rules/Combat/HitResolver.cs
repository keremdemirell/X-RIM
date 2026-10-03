using System;
using System.Collections.Generic;
using XRim.Core;
using XRim.Rules.Damage;
using XRim.Rules.Events;
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
    /// </list>
    /// </summary>
    public sealed class HitResolver
    {
        private static readonly Side[] BothSides = { Side.Left, Side.Right };

        private readonly MatchState _state;
        private readonly RulesSettings _settings;
        private readonly RulePolicies _policies;
        private readonly DamageCalculator _calculator;
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
            _calculator = new DamageCalculator(Guard.NotNull(policies.InstantKo, nameof(RulePolicies.InstantKo)));
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
            var cancel = new PerSide<bool>(false, false);
            var died = new PerSide<bool>(false, false);
            var landed = new List<LandedHit>();
            var events = new List<MatchEvent>();

            foreach (HitFacts hit in hits)
            {
                if (!CanLand(hit, deadBefore, weaponsTravelling)) continue;

                LandedHit result = Land(hit, events);
                landed.Add(result);
                Side victim = hit.Victim;
                if (result.KillsVictim) died[victim] = true;
                if (weaponsTravelling[victim] && (result.KillsVictim || Interrupts(hit, result.Damage))) cancel[victim] = true;
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

            return new HitResolution(time, landed, interrupted, dead, events);
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

        private LandedHit Land(HitFacts hit, List<MatchEvent> events)
        {
            TurnAttack attack = Attacks[hit.Attacker];
            FighterState victim = _state.Fighters[hit.Victim];
            float speedAtHit = attack.SpeedFraction;
            var context = new DamageContext(hit, attack.Weapon, _settings, attack.BodyMove, attack.CrushedThrough, speedAtHit,
                attack.ShieldBlockDamageMultiplier);
            DamageResult damage = _calculator.Calculate(context, _policies.DamageModifiers, victim);

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

            return new LandedHit(hit, damage, speedAtHit, attack.SpeedFraction, attack.IsStopped, kills);
        }

        private bool Interrupts(HitFacts hit, DamageResult damage)
        {
            FighterState victim = _state.Fighters[hit.Victim];
            BodyPart weaponArm = victim.HasDominantArm ? BodyParts.DominantArm(victim.Handedness) : BodyParts.OffArm(victim.Handedness);
            TurnAttack victimAttack = Attacks[hit.Victim];
            if (victimAttack.Weapon == null) return false;

            var check = new InterruptCheck(hit, damage, victimAttack.Weapon, hit.Part == weaponArm, _settings.Damage);
            return _policies.Interrupt.Interrupts(check);
        }
    }
}
