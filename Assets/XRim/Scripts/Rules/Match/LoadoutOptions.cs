using System.Collections.Generic;
using XRim.Core;
using XRim.Rules.Settings;

namespace XRim.Rules.Match
{
    /// <summary>
    /// What a player can pick from before a match (GDD §6): the owned weapons and shields. The shield counts as a weapon and takes
    /// a slot (Decided). The severed-limb club and the armless body blow are never picked: they appear in a match only after a
    /// dismemberment (§12, <see cref="LoadoutSettings.SeveredLimbIsExtraOption"/>).
    /// </summary>
    public static class LoadoutOptions
    {
        /// <summary>True for a weapon or a shield: the kinds a loadout slot can hold.</summary>
        public static bool IsPickable(WeaponStats item)
        {
            Guard.NotNull(item, nameof(item));
            return item.Kind == WeaponKind.Weapon || item.Kind == WeaponKind.Shield;
        }

        /// <summary>The pickable weapons this player owns, in the order of the weapon list.</summary>
        public static List<WeaponId> Pickable(RulesSettings settings, IWeaponOwnership ownership)
        {
            Guard.NotNull(settings, nameof(settings));
            Guard.NotNull(ownership, nameof(ownership));
            var options = new List<WeaponId>();
            foreach (WeaponStats weapon in settings.Weapons)
            {
                if (IsPickable(weapon) && ownership.Owns(weapon.WeaponId)) options.Add(weapon.WeaponId);
            }

            return options;
        }

        /// <summary>
        /// True when some valid loadout exists: enough owned weapons for every slot, and a shield among them when the settings
        /// require one.
        /// </summary>
        public static bool CanFormALoadout(RulesSettings settings, IWeaponOwnership ownership)
        {
            List<WeaponId> options = Pickable(settings, ownership);
            if (options.Count < RuleConstants.LoadoutSlotCount) return false;
            return !settings.Loadout.RequireShieldSlot || options.Exists(id => IsShield(settings, id));
        }

        /// <summary>
        /// The loadout the picker starts with: the default loadout (D8, rapier, mace, shield), keeping the entries that are owned
        /// and pickable, topped up from the other options in list order, with a shield swapped in for the last slot when one is
        /// required and missing. One tap starts a match. It is valid whenever <see cref="CanFormALoadout"/> is.
        /// </summary>
        public static List<WeaponId> DefaultFor(RulesSettings settings, IWeaponOwnership ownership)
        {
            List<WeaponId> options = Pickable(settings, ownership);
            var loadout = new List<WeaponId>();
            foreach (string id in settings.Loadout.DefaultLoadoutWeaponIds)
            {
                var weapon = new WeaponId(id);
                if (options.Contains(weapon) && !loadout.Contains(weapon) && loadout.Count < RuleConstants.LoadoutSlotCount)
                {
                    loadout.Add(weapon);
                }
            }

            foreach (WeaponId option in options)
            {
                if (loadout.Count >= RuleConstants.LoadoutSlotCount) break;
                if (!loadout.Contains(option)) loadout.Add(option);
            }

            if (settings.Loadout.RequireShieldSlot && loadout.Count > 0 && !loadout.Exists(id => IsShield(settings, id)))
            {
                WeaponId shield = options.Find(id => IsShield(settings, id));
                if (!shield.IsEmpty) loadout[loadout.Count - 1] = shield;
            }

            return loadout;
        }

        private static bool IsShield(RulesSettings settings, WeaponId id) => settings.FindWeapon(id)?.Kind == WeaponKind.Shield;
    }
}
