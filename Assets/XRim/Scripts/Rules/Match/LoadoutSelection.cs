using System.Collections.Generic;
using XRim.Core;
using XRim.Rules.Settings;

namespace XRim.Rules.Match
{
    /// <summary>
    /// The pre-match pick (GDD §6): each player locks in 3 owned weapons, the shield counting as one, without seeing the other's
    /// picks; once both are locked in, both loadouts are revealed (Decided). A pure model: the picker screen, a bot and a server
    /// all drive the same rules. A locked-in loadout is valid (<see cref="LoadoutValidator"/>) and cannot change.
    /// </summary>
    public sealed class LoadoutSelection
    {
        private readonly RulesSettings _settings;
        private readonly IWeaponOwnership _ownership;
        private readonly PerSide<IReadOnlyList<WeaponId>> _lockedIn = new PerSide<IReadOnlyList<WeaponId>>(null, null);

        public LoadoutSelection(RulesSettings settings, IWeaponOwnership ownership)
        {
            _settings = Guard.NotNull(settings, nameof(settings));
            _ownership = Guard.NotNull(ownership, nameof(ownership));
        }

        /// <summary>What can be picked: the owned weapons and shields.</summary>
        public IReadOnlyList<WeaponId> Choices => LoadoutOptions.Pickable(_settings, _ownership);

        public bool IsLockedIn(Side side) => _lockedIn[side] != null;

        public bool IsComplete => IsLockedIn(Side.Left) && IsLockedIn(Side.Right);

        /// <summary>
        /// Locks in this side's loadout. Returns false, with the reasons, when it is refused: nothing is stored and the side can
        /// try again. A side that is already locked in cannot change.
        /// </summary>
        public bool TryLockIn(Side side, IReadOnlyList<WeaponId> loadout, out IReadOnlyList<string> issues)
        {
            Guard.NotNull(loadout, nameof(loadout));
            var found = new List<string>();
            if (IsLockedIn(side)) found.Add("This side's loadout is already locked in.");
            LoadoutValidator.Validate(loadout, _settings, _ownership, found);
            issues = found;
            if (found.Count > 0) return false;

            _lockedIn[side] = new List<WeaponId>(loadout);
            return true;
        }

        /// <summary>This side's own locked-in loadout; null while it is still picking.</summary>
        public IReadOnlyList<WeaponId> OwnLoadout(Side side) => _lockedIn[side];

        /// <summary>
        /// What <paramref name="viewer"/> may see of the other side's loadout: nothing (null) until both are locked in (GDD §6:
        /// "both players see each other's loadout at the start of the match").
        /// </summary>
        public IReadOnlyList<WeaponId> OpponentLoadoutSeenBy(Side viewer) => IsComplete ? _lockedIn[viewer.Opponent()] : null;

        /// <summary>Both loadouts, once both sides are locked in.</summary>
        public LoadoutReveal Reveal()
        {
            if (!IsComplete) throw new System.InvalidOperationException("Both loadouts must be locked in before they are revealed.");
            return new LoadoutReveal(new PerSide<IReadOnlyList<WeaponId>>(_lockedIn.Left, _lockedIn.Right));
        }

        /// <summary>The fighters the match starts with, from the locked-in loadouts and each side's handedness.</summary>
        public PerSide<FighterSetup> CreateFighters(PerSide<Handedness> handedness)
        {
            Guard.NotNull(handedness, nameof(handedness));
            LoadoutReveal reveal = Reveal();
            return PerSide<FighterSetup>.Create(side => new FighterSetup(handedness[side], reveal.Of(side)));
        }
    }
}
