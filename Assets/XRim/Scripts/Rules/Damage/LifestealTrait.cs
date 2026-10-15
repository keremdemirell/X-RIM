using System;
using XRim.Rules.Match;

namespace XRim.Rules.Damage
{
    /// <summary>
    /// Lifesteal (GDD §15, TBD; flag <c>TraitSettings.LifestealEnabled</c>, off): after
    /// <c>TraitSettings.LifestealHitsNeeded</c> (3) successful hits with a weapon that carries it, the dummy recovers
    /// <c>LifestealRecoveredFraction</c> of the HP damage those hits dealt, and the count starts again. Counted over all hits
    /// (<see cref="LifestealHitCounting.Total"/>) or only hits in a row (<see cref="LifestealHitCounting.Consecutive"/>, see
    /// <see cref="WeaponTraitRules.EndTurn"/>). It never changes the damage of a hit.
    /// </summary>
    public sealed class LifestealTrait : IWeaponTrait
    {
        public string Id => WeaponTraitIds.Lifesteal;

        public float Modify(float damage, DamageContext context) => damage;

        public float OnHitLanded(TraitHitContext hit)
        {
            if (!hit.Settings.LifestealEnabled || !WeaponTraitRules.Carries(hit.Weapon, Id)) return 0f;

            FighterState attacker = hit.Attacker;
            attacker.LifestealHitCount++;
            attacker.LifestealDamageDealt += hit.Damage.HpDamage;
            if (attacker.LifestealHitCount < Math.Max(1, hit.Settings.LifestealHitsNeeded)) return 0f;

            float recovered = attacker.LifestealDamageDealt * Math.Max(0f, hit.Settings.LifestealRecoveredFraction);
            attacker.LifestealHitCount = 0;
            attacker.LifestealDamageDealt = 0f;
            return recovered;
        }
    }
}
