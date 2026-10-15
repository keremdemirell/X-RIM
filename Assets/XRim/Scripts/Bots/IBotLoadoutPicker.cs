using System.Collections.Generic;
using XRim.Core;
using XRim.Rules;
using XRim.Rules.Match;
using XRim.Rules.Settings;

namespace XRim.Bots
{
    /// <summary>How a bot fills its 3 loadout slots before a match (GDD §6). Whatever it returns passes <see cref="LoadoutValidator"/>.</summary>
    public interface IBotLoadoutPicker
    {
        /// <summary>
        /// Picks a valid loadout from the weapons the bot owns. Returns false, with nothing picked, when no valid loadout exists
        /// (too few owned weapons, or a required shield it does not own).
        /// </summary>
        bool TryPick(RulesSettings settings, IWeaponOwnership ownership, IRandom random, out IReadOnlyList<WeaponId> loadout);
    }
}
