using System;
using XRim.Core;
using XRim.Rules.Combat;
using XRim.Rules.Match;
using XRim.Rules.Settings;

namespace XRim.Rules.Damage
{
    /// <summary>
    /// D27 default (designer, 2026-10-03): a single hit cannot take a dummy from full HP to 0; its damage is clamped to leave
    /// 1 HP. Later hits can finish a damaged dummy. It covers every zone, as the D27 wording does, not only the head. A dummy
    /// in sudden death is not at full HP, so its first hit still wins (§14).
    /// </summary>
    public sealed class NoKoFromFullHpPolicy : IInstantKoPolicy
    {
        /// <summary>D27 proposal: what a clamped hit leaves.</summary>
        private const float HpLeftAfterClampedHit = 1f;

        public float LimitHpDamage(float damage, HitFacts hit, FighterState victim, DamageSettings settings)
        {
            Guard.NotNull(victim, nameof(victim));
            Guard.NotNull(settings, nameof(settings));
            bool atFullHp = victim.Hp >= settings.MaxHp;
            if (!atFullHp || victim.Hp <= HpLeftAfterClampedHit) return damage;
            return Math.Min(damage, victim.Hp - HpLeftAfterClampedHit);
        }
    }
}
