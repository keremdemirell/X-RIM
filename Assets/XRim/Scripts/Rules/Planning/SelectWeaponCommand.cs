namespace XRim.Rules.Planning
{
    /// <summary>
    /// Switch to a loadout weapon (GDD §6, Decided): shown to the opponent immediately, erases the drawn
    /// path, and is refused during the final lock-out window.
    /// </summary>
    public sealed class SelectWeaponCommand : PlanningCommand
    {
        public WeaponId Weapon { get; }

        public SelectWeaponCommand(WeaponId weapon)
        {
            Weapon = weapon;
        }
    }
}
