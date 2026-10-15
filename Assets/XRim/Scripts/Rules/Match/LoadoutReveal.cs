using System.Collections.Generic;
using XRim.Core;

namespace XRim.Rules.Match
{
    /// <summary>
    /// Both loadouts, shown to both players at the start of the match (GDD §6, Decided). Only a finished
    /// <see cref="LoadoutSelection"/> makes one, so neither player sees the other's picks while still picking.
    /// </summary>
    public sealed class LoadoutReveal
    {
        private readonly PerSide<IReadOnlyList<WeaponId>> _loadouts;

        public LoadoutReveal(PerSide<IReadOnlyList<WeaponId>> loadouts)
        {
            _loadouts = Guard.NotNull(loadouts, nameof(loadouts));
        }

        public IReadOnlyList<WeaponId> Of(Side side) => _loadouts[side];

        public IReadOnlyList<WeaponId> Left => _loadouts.Left;

        public IReadOnlyList<WeaponId> Right => _loadouts.Right;
    }
}
