using System.Collections.Generic;
using XRim.Core;
using XRim.Rules.Settings;

namespace XRim.Rules.Match
{
    /// <summary>
    /// Checks a picked loadout before a match (GDD §6): exactly <see cref="RuleConstants.LoadoutSlotCount"/> slots
    /// (Decided), every weapon known to the settings and none picked twice, and a shield among them when
    /// <see cref="LoadoutSettings.RequireShieldSlot"/> asks for one (D8, not decided: off by default).
    /// Severed limbs join a loadout later as temporary extras (§12), not through the picked slots.
    /// </summary>
    public static class LoadoutValidator
    {
        /// <summary>Appends a message for every problem; appends nothing when the loadout is fine.</summary>
        public static void Validate(IReadOnlyList<WeaponId> loadout, RulesSettings settings, ICollection<string> issues)
        {
            Guard.NotNull(loadout, nameof(loadout));
            Guard.NotNull(settings, nameof(settings));
            Guard.NotNull(issues, nameof(issues));

            if (loadout.Count != RuleConstants.LoadoutSlotCount)
            {
                issues.Add($"A loadout takes exactly {RuleConstants.LoadoutSlotCount} weapons, not {loadout.Count}.");
            }

            var seen = new HashSet<WeaponId>();
            bool hasShield = false;
            foreach (WeaponId weapon in loadout)
            {
                WeaponStats stats = settings.FindWeapon(weapon);
                if (stats == null) issues.Add($"The loadout has an unknown weapon '{weapon}'.");
                else if (stats.Kind == WeaponKind.Shield) hasShield = true;

                if (!seen.Add(weapon)) issues.Add($"The loadout has '{weapon}' twice.");
            }

            if (settings.Loadout.RequireShieldSlot && !hasShield)
            {
                issues.Add("The loadout must include a shield (LoadoutSettings.RequireShieldSlot).");
            }
        }
    }
}
