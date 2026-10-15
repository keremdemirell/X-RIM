using System.Collections.Generic;
using XRim.Rules;
using XRim.Rules.Match;

namespace XRim.Tests.EditMode.Rules.Match
{
    /// <summary>A player who owns only these weapons (the profile of Session 18, in a test).</summary>
    internal sealed class OwnedWeapons : IWeaponOwnership
    {
        private readonly HashSet<WeaponId> _owned;

        public OwnedWeapons(params WeaponId[] owned)
        {
            _owned = new HashSet<WeaponId>(owned);
        }

        public bool Owns(WeaponId weapon) => _owned.Contains(weapon);
    }
}
