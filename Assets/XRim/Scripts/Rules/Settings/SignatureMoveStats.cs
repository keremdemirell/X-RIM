using System;
using System.Collections.Generic;
using XRim.Core;
using XRim.Core.Gdd;

namespace XRim.Rules.Settings
{
    /// <summary>
    /// One signature (joker) move (GDD §8, Decided concept): a preset pattern that draws itself when
    /// tapped and carries a special effect. Names are absurd, e.g. "Head Impact Simulation #404".
    /// </summary>
    [Serializable]
    public sealed class SignatureMoveStats
    {
        public string Id = string.Empty;
        public string DisplayName = string.Empty;

        /// <summary>Preset path in the fighter's torso frame, arena units.</summary>
        public List<Vec2> PresetPath = new List<Vec2>();

        /// <summary>Empty = works with any weapon.</summary>
        [GddTbd("§8", "Weapon ties and ink use of signature moves")]
        public List<string> AllowedWeaponIds = new List<string>();

        [GddTbd("§8", "Weapon ties and ink use of signature moves")]
        public bool ConsumesInk = true;

        [GddTbd("§8", "Signature moves and body moves", Proposal = "Replaces only the weapon path")]
        public bool OverridesBodyMove = false;

        public BodyMove BodyMoveOverride = BodyMove.None;

        [GddTbd("§8", "Catalogue of signature moves")]
        public string SpecialEffectId = string.Empty;
    }
}
