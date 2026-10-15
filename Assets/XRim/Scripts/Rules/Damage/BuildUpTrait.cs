using System;

namespace XRim.Rules.Damage
{
    /// <summary>
    /// Build-up (GDD §15, TBD; flag <c>TraitSettings.BuildUpEnabled</c>, off): the more HP damage the dummy has taken in total, the
    /// harder a weapon that carries it hits: damage × (1 + growth × damage taken), the bonus capped at
    /// <c>BuildUpMaxBonusFraction</c>. It scales with the damage taken, never with the HP left (a low-HP rage is Rejected, §20), so
    /// healing does not undo it.
    /// </summary>
    public sealed class BuildUpTrait : IWeaponTrait
    {
        public string Id => WeaponTraitIds.BuildUp;

        public float Modify(float damage, DamageContext context)
        {
            var settings = context.Settings.Traits;
            if (!settings.BuildUpEnabled || !WeaponTraitRules.Carries(context.Weapon, Id)) return damage;

            float bonus = Math.Max(0f, settings.BuildUpGrowthPerHpDamageTaken) * Math.Max(0f, context.AttackerHpDamageTaken);
            return damage * (1f + Math.Min(bonus, Math.Max(0f, settings.BuildUpMaxBonusFraction)));
        }

        public float OnHitLanded(TraitHitContext hit) => 0f;
    }
}
