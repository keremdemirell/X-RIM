using System.Collections.Generic;
using XRim.Core;
using XRim.Rules;
using XRim.Rules.Match;
using XRim.Rules.Settings;

namespace XRim.Bots
{
    /// <summary>
    /// The debug bot's loadout (D25: a developer opponent, not a game mode): 3 different owned weapons at random, a shield among
    /// them when the settings require one, in a random order (the first is the weapon the bot starts with). Always valid, so a
    /// match against it never starts with a refused loadout, and every owned weapon gets played across a batch of matches.
    /// </summary>
    public sealed class RandomLoadoutPicker : IBotLoadoutPicker
    {
        public bool TryPick(RulesSettings settings, IWeaponOwnership ownership, IRandom random, out IReadOnlyList<WeaponId> loadout)
        {
            Guard.NotNull(settings, nameof(settings));
            Guard.NotNull(ownership, nameof(ownership));
            Guard.NotNull(random, nameof(random));
            loadout = null;
            if (!LoadoutOptions.CanFormALoadout(settings, ownership)) return false;

            List<WeaponId> options = LoadoutOptions.Pickable(settings, ownership);
            var picked = new List<WeaponId>(RuleConstants.LoadoutSlotCount);
            if (settings.Loadout.RequireShieldSlot)
            {
                List<WeaponId> shields = options.FindAll(id => settings.FindWeapon(id).Kind == WeaponKind.Shield);
                WeaponId shield = shields[random.NextInt(0, shields.Count)];
                picked.Add(shield);
                options.Remove(shield);
            }

            while (picked.Count < RuleConstants.LoadoutSlotCount)
            {
                int index = random.NextInt(0, options.Count);
                picked.Add(options[index]);
                options.RemoveAt(index);
            }

            for (int i = picked.Count - 1; i > 0; i--)
            {
                int swapWith = random.NextInt(0, i + 1);
                (picked[i], picked[swapWith]) = (picked[swapWith], picked[i]);
            }

            loadout = picked;
            return true;
        }
    }
}
