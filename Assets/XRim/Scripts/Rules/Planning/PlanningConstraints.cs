using System.Collections.Generic;
using XRim.Rules.Settings;
using XRim.Rules.Status;

namespace XRim.Rules.Planning
{
    /// <summary>
    /// Per-player limits for one planning phase. Status effects (stun, stagger) and limb loss change
    /// these rather than adding special cases to the planning rules (GDD §10, §11, §12).
    /// </summary>
    public sealed class PlanningConstraints
    {
        private readonly HashSet<BodyMove> _forbiddenBodyMoves = new HashSet<BodyMove>();
        private readonly HashSet<WeaponId> _forbiddenWeapons = new HashSet<WeaponId>();
        private readonly List<WeaponId> _extraWeapons = new List<WeaponId>();
        private readonly List<StatusKind> _statuses = new List<StatusKind>();
        private bool _allBodyMovesForbidden;

        public float PlanningDurationSeconds { get; set; }
        public float InkLengthMultiplier { get; set; } = 1f;
        public bool CanSwitchWeapon { get; set; } = true;
        public bool CanUseSignature { get; set; } = true;

        /// <summary>False once the dummy has no arm to hold a loadout weapon (§12): only the extra options remain.</summary>
        public bool CanUseLoadoutWeapons { get; set; } = true;

        /// <summary>Temporary options beyond the loadout this turn (§12): the severed-limb club, the armless body blow.</summary>
        public IReadOnlyList<WeaponId> ExtraWeapons => _extraWeapons;

        /// <summary>The status effects shaping this turn (a stun, a stagger), in the order they were applied. For the HUD.</summary>
        public IReadOnlyList<StatusKind> Statuses => _statuses;

        /// <summary>No move is always allowed: the dummy holds its pose (GDD §3).</summary>
        public bool IsBodyMoveAllowed(BodyMove move) =>
            move == BodyMove.None || (!_allBodyMovesForbidden && !_forbiddenBodyMoves.Contains(move));

        public void ForbidBodyMove(BodyMove move) => _forbiddenBodyMoves.Add(move);

        /// <summary>Every body move except none, including moves added to the game later (a stun, D17 default).</summary>
        public void ForbidAllBodyMoves() => _allBodyMovesForbidden = true;

        /// <summary>A loadout weapon that cannot be picked this turn (lost with its arm, G2's option).</summary>
        public void ForbidWeapon(WeaponId weapon) => _forbiddenWeapons.Add(weapon);

        public void AddExtraWeapon(WeaponId weapon)
        {
            if (!_extraWeapons.Contains(weapon)) _extraWeapons.Add(weapon);
        }

        /// <summary>Whether the weapon can be held this turn: an extra option, or a loadout weapon that is neither forbidden nor out of hand.</summary>
        public bool IsWeaponAvailable(WeaponId weapon, IReadOnlyList<WeaponId> loadout)
        {
            if (_extraWeapons.Contains(weapon)) return true;
            if (!CanUseLoadoutWeapons || _forbiddenWeapons.Contains(weapon) || loadout == null) return false;
            for (int i = 0; i < loadout.Count; i++)
            {
                if (loadout[i] == weapon) return true;
            }

            return false;
        }

        /// <summary>Every weapon that can be held this turn: the available loadout weapons in slot order, then the extra options.</summary>
        public List<WeaponId> AvailableWeapons(IReadOnlyList<WeaponId> loadout)
        {
            var available = new List<WeaponId>();
            if (CanUseLoadoutWeapons && loadout != null)
            {
                foreach (WeaponId weapon in loadout)
                {
                    if (!_forbiddenWeapons.Contains(weapon) && !available.Contains(weapon)) available.Add(weapon);
                }
            }

            foreach (WeaponId weapon in _extraWeapons)
            {
                if (!available.Contains(weapon)) available.Add(weapon);
            }

            return available;
        }

        public void AddStatus(StatusKind kind) => _statuses.Add(kind);

        public static PlanningConstraints CreateDefault(MatchSettings settings) =>
            new PlanningConstraints { PlanningDurationSeconds = settings.PlanningDurationSeconds };
    }
}
