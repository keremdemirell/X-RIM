using XRim.Core;
using XRim.Economy;
using XRim.Rules;
using XRim.Rules.Match;

namespace XRim.Profile
{
    /// <summary>
    /// Which weapons a player may put in a loadout (GDD §6: three slots from the weapons they own), read from their inventory. A shield is
    /// its own kind of unlock in the shop (<see cref="GameplayUnlockKind.Shield"/>) but a weapon to the rules, so either kind counts.
    /// </summary>
    public sealed class ProfileWeaponOwnership : IWeaponOwnership
    {
        private readonly Inventory _inventory;

        public ProfileWeaponOwnership(Inventory inventory)
        {
            _inventory = Guard.NotNull(inventory, nameof(inventory));
        }

        public bool Owns(WeaponId weapon) =>
            _inventory.Owns(GameplayUnlockKind.Weapon, weapon.Value) || _inventory.Owns(GameplayUnlockKind.Shield, weapon.Value);
    }
}
