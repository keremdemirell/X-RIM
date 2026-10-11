using System;
using System.Collections.Generic;
using XRim.Core.Gdd;

namespace XRim.Rules.Settings
{
    /// <summary>Loadout rules (GDD §6, §12). The slot count is Decided: see <see cref="RuleConstants.LoadoutSlotCount"/>.</summary>
    [Serializable]
    public sealed class LoadoutSettings
    {
        [GddTbd("§6", "Must one loadout slot be a shield?")]
        public bool RequireShieldSlot = false;

        /// <summary>
        /// True: the severed-limb club is a temporary extra option beyond the 3 slots while a free limb lies on the floor. False
        /// (S4): it takes a slot, so picking it up gives up the weapon held before it for the rest of the match.
        /// </summary>
        [GddTbd("§12", "Severed limb stats and slot",
            Proposal = "Designer 2026-10-10 (recommended default): a temporary extra option, with the placeholder stats")]
        public bool SeveredLimbIsExtraOption = true;

        [GddTbd("§6", "Starting weapons and default loadout")]
        [Placeholder("§6 default loadout for new players is TBD")]
        public List<string> DefaultLoadoutWeaponIds = new List<string> { "rapier", "mace", "shield" };
    }
}
