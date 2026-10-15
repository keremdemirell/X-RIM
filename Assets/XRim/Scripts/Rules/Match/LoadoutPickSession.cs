using System.Collections.Generic;
using XRim.Core;
using XRim.Rules.Settings;

namespace XRim.Rules.Match
{
    /// <summary>
    /// The flow of the pre-match pick (GDD §6), without any screen: each human in turn (a hand-over first in hot-seat, so the next
    /// player never sees the last one's picks), toggling weapons into 3 slots and locking them in, then the reveal of both loadouts
    /// and a confirmation that starts the match. Bots are locked into the <see cref="LoadoutSelection"/> before it begins. The
    /// picker screen only draws what this says, so the rules of the pick are tested here without an Editor.
    /// A human starts with a loadout filled in (their last picks when valid, else the default, D8), so one tap starts a match.
    /// The order of the picks matters: the first slot is the weapon the dummy starts the match with.
    /// </summary>
    public sealed class LoadoutPickSession
    {
        private readonly RulesSettings _settings;
        private readonly IWeaponOwnership _ownership;
        private readonly LoadoutSelection _selection;
        private readonly List<Side> _humans;
        private readonly bool _handOverBetweenPlayers;
        private readonly PerSide<IReadOnlyList<WeaponId>> _previousPicks;
        private readonly List<WeaponId> _choices;
        private readonly List<WeaponId> _picks = new List<WeaponId>();
        private int _humanIndex;

        /// <param name="selection">Already holds the bots' locked-in loadouts; every human side must still be open.</param>
        /// <param name="humanSides">Who picks, in the order they pick.</param>
        /// <param name="handOverBetweenPlayers">True in hot-seat: the device changes hands before each player's pick.</param>
        /// <param name="previousPicks">Each side's picks of the last match, to start from (a rematch); null for none.</param>
        public LoadoutPickSession(RulesSettings settings, IWeaponOwnership ownership, LoadoutSelection selection,
            IReadOnlyList<Side> humanSides, bool handOverBetweenPlayers, PerSide<IReadOnlyList<WeaponId>> previousPicks = null)
        {
            _settings = Guard.NotNull(settings, nameof(settings));
            _ownership = Guard.NotNull(ownership, nameof(ownership));
            _selection = Guard.NotNull(selection, nameof(selection));
            _choices = LoadoutOptions.Pickable(_settings, _ownership);
            Guard.NotNull(humanSides, nameof(humanSides));
            _humans = new List<Side>(humanSides);
            _handOverBetweenPlayers = handOverBetweenPlayers;
            _previousPicks = previousPicks;

            foreach (Side side in _humans)
            {
                if (_selection.IsLockedIn(side)) throw new System.ArgumentException($"The {side} side is a human but already locked in.");
            }

            if (_humans.Count > 0) BeginHuman();
            else Stage = _selection.IsComplete ? LoadoutPickStage.Reveal : throw new System.ArgumentException("Nobody picks and a side is open.");
        }

        public LoadoutPickStage Stage { get; private set; }

        /// <summary>The human who is picking now (or about to take the device); only in the hand-over and picking stages.</summary>
        public Side PickingSide =>
            Stage == LoadoutPickStage.HandOver || Stage == LoadoutPickStage.Picking
                ? _humans[_humanIndex]
                : throw new System.InvalidOperationException("Nobody is picking in this stage.");

        /// <summary>What can be picked: the owned weapons and shields, in the order of the weapon list.</summary>
        public IReadOnlyList<WeaponId> Choices => _choices;

        /// <summary>The picking human's slots so far, in the order picked (the first is the starting weapon).</summary>
        public IReadOnlyList<WeaponId> Picks => _picks;

        /// <summary>True when the picks so far are a valid loadout and <see cref="Confirm"/> would lock them in.</summary>
        public bool CanConfirm => Stage == LoadoutPickStage.Picking && Problems().Count == 0;

        /// <summary>
        /// Why a full set of picks is refused (for example the required shield), or null while it is fine or still being filled.
        /// </summary>
        public string Problem
        {
            get
            {
                if (Stage != LoadoutPickStage.Picking || _picks.Count < RuleConstants.LoadoutSlotCount) return null;
                List<string> problems = Problems();
                return problems.Count > 0 ? problems[0] : null;
            }
        }

        /// <summary>Both loadouts, from the reveal on.</summary>
        public LoadoutReveal Result =>
            Stage == LoadoutPickStage.Reveal || Stage == LoadoutPickStage.Done
                ? _selection.Reveal()
                : throw new System.InvalidOperationException("The loadouts are not revealed yet.");

        /// <summary>The player has the device (hot-seat): picking starts. Returns false outside the hand-over.</summary>
        public bool ConfirmHandOver()
        {
            if (Stage != LoadoutPickStage.HandOver) return false;
            Stage = LoadoutPickStage.Picking;
            return true;
        }

        /// <summary>
        /// Puts a weapon into a slot, or back. Returns false (nothing changes) when it is not an option, or when all 3 slots are
        /// full and it is not one of them: put one back first.
        /// </summary>
        public bool Toggle(WeaponId weapon)
        {
            if (Stage != LoadoutPickStage.Picking || !_choices.Contains(weapon)) return false;
            if (_picks.Remove(weapon)) return true;
            if (_picks.Count >= RuleConstants.LoadoutSlotCount) return false;
            _picks.Add(weapon);
            return true;
        }

        /// <summary>
        /// Locks the picks in and moves to the next human, or to the reveal after the last. Returns false when they are not a valid
        /// loadout yet (see <see cref="Problem"/>).
        /// </summary>
        public bool Confirm()
        {
            if (Stage != LoadoutPickStage.Picking) return false;
            if (!_selection.TryLockIn(PickingSide, _picks, out _)) return false;

            _humanIndex++;
            if (_humanIndex < _humans.Count) BeginHuman();
            else Stage = LoadoutPickStage.Reveal;
            return true;
        }

        /// <summary>The players have seen both loadouts: the match can start. Returns false outside the reveal.</summary>
        public bool ConfirmReveal()
        {
            if (Stage != LoadoutPickStage.Reveal) return false;
            Stage = LoadoutPickStage.Done;
            return true;
        }

        private void BeginHuman()
        {
            _picks.Clear();
            _picks.AddRange(StartingPicks(PickingSide));
            Stage = _handOverBetweenPlayers ? LoadoutPickStage.HandOver : LoadoutPickStage.Picking;
        }

        /// <summary>The side's last picks when they are still a valid loadout, else the default for the owned weapons.</summary>
        private List<WeaponId> StartingPicks(Side side)
        {
            IReadOnlyList<WeaponId> previous = _previousPicks?[side];
            if (previous != null)
            {
                var issues = new List<string>();
                LoadoutValidator.Validate(previous, _settings, _ownership, issues);
                if (issues.Count == 0) return new List<WeaponId>(previous);
            }

            return LoadoutOptions.DefaultFor(_settings, _ownership);
        }

        private List<string> Problems()
        {
            var issues = new List<string>();
            LoadoutValidator.Validate(_picks, _settings, _ownership, issues);
            return issues;
        }
    }
}
