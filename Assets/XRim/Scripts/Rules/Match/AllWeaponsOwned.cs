namespace XRim.Rules.Match
{
    /// <summary>
    /// Every weapon is owned: the prototype's ownership until the profile and economy exist (Session 18). It lets a tester pick the
    /// sword and the spear, which a new player will have to unlock.
    /// </summary>
    public sealed class AllWeaponsOwned : IWeaponOwnership
    {
        public bool Owns(WeaponId weapon) => true;
    }
}
