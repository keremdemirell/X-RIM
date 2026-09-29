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

        [GddTbd("§12", "Severed limb stats and slot", Proposal = "Temporary extra option")]
        public bool SeveredLimbIsExtraOption = true;

        [GddTbd("§6", "Starting weapons and default loadout")]
        [Placeholder("§6 default loadout for new players is TBD")]
        public List<string> DefaultLoadoutWeaponIds = new List<string> { "rapier", "mace", "shield" };
    }
}
