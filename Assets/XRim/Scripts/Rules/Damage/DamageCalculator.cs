using System;
using System.Collections.Generic;
using XRim.Core;
using XRim.Rules.Limbs;
using XRim.Rules.Match;
using XRim.Rules.Settings;

namespace XRim.Rules.Damage
{
    /// <summary>
    /// Damage = BaseDamage(weapon) × ZoneMultiplier × Modifiers (GDD §11, Decided). Designer-set base
    /// damage, never physics energy (kinetic-energy damage is Rejected). Then:
    /// <list type="bullet">
    /// <item>the HP it removes, through the no-instant-KO rule (<see cref="IInstantKoPolicy"/>, D27);</item>
    /// <item>for an arm or leg, the limb damage it adds, at most the per-hit cap (§11), and whether that brings the limb to
    /// its durability (Session 11 severs it);</item>
    /// <item>for the head, whether it stuns: damage strictly above the stun threshold, measured before the D27 clamp.</item>
    /// </list>
    /// The calculation reads the victim but never changes it; <see cref="ApplyTo"/> does.
    /// </summary>
    public sealed class DamageCalculator
    {
        private readonly IInstantKoPolicy _instantKo;
        private readonly LimbRules _limbs = new LimbRules();

        public DamageCalculator(IInstantKoPolicy instantKo)
        {
            _instantKo = Guard.NotNull(instantKo, nameof(instantKo));
        }

        public DamageResult Calculate(DamageContext context, IReadOnlyList<IDamageModifier> modifiers, FighterState victim)
        {
            Guard.NotNull(context, nameof(context));
            Guard.NotNull(modifiers, nameof(modifiers));
            Guard.NotNull(victim, nameof(victim));
            DamageSettings settings = context.Settings.Damage;

            float damage = context.Weapon.BaseDamage * context.ZoneMultiplier;
            foreach (IDamageModifier modifier in modifiers)
            {
                damage = modifier.Modify(damage, context);
            }

            damage = Math.Max(0f, damage);
            float hpDamage = _instantKo.LimitHpDamage(damage, context.Hit, victim, settings);

            BodyPart part = context.Hit.Part;
            float limbDamage = 0f;
            bool seversLimb = false;
            if (part.IsSeverable() && !victim.IsSevered(part))
            {
                float durability = _limbs.DurabilityOf(part, settings);
                float before = victim.GetLimbDamage(part);
                limbDamage = _limbs.CapLimbDamage(damage, durability, settings);
                seversLimb = before < durability && before + limbDamage >= durability;
            }

            bool stuns = context.Zone == HitZone.Head && damage > settings.HeadStunThreshold;
            return new DamageResult(damage, hpDamage, limbDamage, stuns, seversLimb);
        }

        /// <summary>Removes the hit's HP and adds its limb damage, which never heals (GDD §11).</summary>
        public static void ApplyTo(FighterState victim, BodyPart part, DamageResult result)
        {
            Guard.NotNull(victim, nameof(victim));
            victim.Hp -= result.HpDamage;
            if (result.LimbDamage > 0f) victim.SetLimbDamage(part, victim.GetLimbDamage(part) + result.LimbDamage);
        }
    }
}
