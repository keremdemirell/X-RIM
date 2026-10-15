using XRim.Core.Gdd;

namespace XRim.Rules.Match
{
    /// <summary>
    /// Which weapons a player may put into a loadout (GDD §6: "3 slots from the weapons they own"). Unlockable weapons are bought
    /// with earned currency only (§16), so ownership comes from the profile and economy of Session 18; the rules only ask.
    /// </summary>
    [GddTbd("§6", "Starting weapons for a new player",
        Proposal = "Designer 2026-10-15 (recommended default): rapier, mace and shield; sword and spear are unlockables (Session 18)")]
    public interface IWeaponOwnership
    {
        bool Owns(WeaponId weapon);
    }
}
