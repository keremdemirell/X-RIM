using XRim.Core;
using XRim.Rules.Match;
using XRim.Rules.Settings;

namespace XRim.Rules.Damage
{
    /// <summary>The rules around weapon traits that belong to no one trait (GDD §15).</summary>
    public static class WeaponTraitRules
    {
        /// <summary>True when the weapon lists this trait.</summary>
        public static bool Carries(WeaponStats weapon, string traitId)
        {
            if (weapon == null || weapon.TraitIds == null) return false;
            foreach (string id in weapon.TraitIds)
            {
                if (string.Equals(id, traitId, System.StringComparison.Ordinal)) return true;
            }

            return false;
        }

        /// <summary>
        /// The end of a turn, for one dummy: the bookkeeping that needs the whole turn. With lifesteal counting hits in a row
        /// (<see cref="LifestealHitCounting.Consecutive"/>), a turn in which the dummy attacked with a weapon that carries it and
        /// landed nothing starts the count again. Always clears the turn's own "landed a hit" mark.
        /// </summary>
        /// <param name="attackedWith">The weapon the dummy drew a path with this turn; null when it did not attack.</param>
        public static void EndTurn(FighterState fighter, WeaponStats attackedWith, TraitSettings settings)
        {
            Guard.NotNull(fighter, nameof(fighter));
            Guard.NotNull(settings, nameof(settings));
            bool missed = attackedWith != null && !fighter.HitLandedThisTurn;
            if (missed && settings.LifestealEnabled && settings.LifestealHitCounting == LifestealHitCounting.Consecutive &&
                Carries(attackedWith, WeaponTraitIds.Lifesteal))
            {
                fighter.LifestealHitCount = 0;
                fighter.LifestealDamageDealt = 0f;
            }

            fighter.HitLandedThisTurn = false;
        }
    }
}
