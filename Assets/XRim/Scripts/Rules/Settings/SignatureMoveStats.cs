using System;
using System.Collections.Generic;
using XRim.Core;
using XRim.Core.Gdd;

namespace XRim.Rules.Settings
{
    /// <summary>
    /// One signature (joker) move (GDD §8, Decided concept): a preset pattern that draws itself when
    /// tapped and carries a special effect. Names are absurd, e.g. "Head Impact Simulation #404". The catalogue is TBD; designer
    /// 2026-10-15: the three placeholder moves of <see cref="GddStartingValues"/>.
    /// </summary>
    [Serializable]
    public sealed class SignatureMoveStats
    {
        public string Id = string.Empty;
        public string DisplayName = string.Empty;

        /// <summary>The GDD has not designed this move: its pattern and numbers are placeholders (like <see cref="WeaponStats.DesignIsTbd"/>).</summary>
        public bool DesignIsTbd;

        /// <summary>
        /// Preset path in the fighter's torso frame, arena units, the way a drawn stroke is (+X toward the opponent). It is built like a
        /// stroke: a lead-in from the weapon tip (D3), clamped to the reach limit (D4).
        /// </summary>
        public List<Vec2> PresetPath = new List<Vec2>();

        /// <summary>The weapons the move works with, by id. Empty = any weapon.</summary>
        [GddTbd("§8", "Weapon ties and ink use of signature moves", Proposal = "Works with any weapon (designer 2026-10-15)")]
        public List<string> AllowedWeaponIds = new List<string>();

        /// <summary>
        /// Whether the move pays the weapon's ink like a drawn path (cut where the ink runs out, rigidity breaking it). Off: the whole
        /// pattern always plays, still clamped to reach.
        /// </summary>
        [GddTbd("§8", "Weapon ties and ink use of signature moves", Proposal = "Ignores the weapon's ink (designer 2026-10-15)")]
        public bool ConsumesInk = false;

        /// <summary>The move defines its own body move: <see cref="BodyMoveOverride"/> replaces the player's swipe.</summary>
        [GddTbd("§8", "Signature moves and body moves", Proposal = "Replaces only the weapon path (designer 2026-10-15)")]
        public bool OverridesBodyMove = false;

        /// <summary>The body move that runs with the pattern when <see cref="OverridesBodyMove"/> is on.</summary>
        public BodyMove BodyMoveOverride = BodyMove.None;

        /// <summary>The id of the special property in the effect catalogue (an <c>ISignatureEffect</c>); empty for none.</summary>
        [GddTbd("§8", "Catalogue of signature moves")]
        public string SpecialEffectId = string.Empty;

        public SignatureMoveId MoveId => new SignatureMoveId(Id);

        /// <summary>The move works with this weapon: it has no ties, or lists the weapon.</summary>
        public bool AllowsWeapon(WeaponId weapon)
        {
            if (AllowedWeaponIds == null || AllowedWeaponIds.Count == 0) return true;
            for (int i = 0; i < AllowedWeaponIds.Count; i++)
            {
                if (string.Equals(AllowedWeaponIds[i], weapon.Value, StringComparison.Ordinal)) return true;
            }

            return false;
        }
    }
}
