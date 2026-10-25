using XRim.Core;
using XRim.Economy;
using XRim.Rules;

namespace XRim.Profile
{
    /// <summary>
    /// Everything one player keeps between sessions (GDD §4, §16): handedness (account-wide, picked at first launch), both wallets, what
    /// they own and have equipped, and their volume settings. Pure data over the Economy types; <see cref="ProfileStore"/> saves it.
    /// </summary>
    public sealed class PlayerProfile
    {
        public Wallet Wallet { get; }
        public Inventory Inventory { get; }
        public AudioVolumes Volumes { get; }

        /// <summary>Null until the player answers the first-launch question (GDD §4, Decided: it applies to the whole account).</summary>
        public Handedness? ChosenHandedness { get; private set; }

        public bool HasChosenHandedness => ChosenHandedness.HasValue;

        public PlayerProfile(Wallet wallet, Inventory inventory, AudioVolumes volumes, Handedness? chosenHandedness)
        {
            Wallet = Guard.NotNull(wallet, nameof(wallet));
            Inventory = Guard.NotNull(inventory, nameof(inventory));
            Volumes = Guard.NotNull(volumes, nameof(volumes));
            ChosenHandedness = chosenHandedness;
        }

        /// <summary>A first launch: empty wallets, the starting weapons and move (designer 2026-10-15 and 2026-10-25), no handedness yet.</summary>
        public static PlayerProfile CreateNew(ShopCatalogue catalogue)
        {
            var profile = new PlayerProfile(new Wallet(), new Inventory(), new AudioVolumes(), null);
            profile.Inventory.GrantStarting(Guard.NotNull(catalogue, nameof(catalogue)));
            return profile;
        }

        /// <summary>The first-launch pick, and later the Settings change (D24: changeable, no separate mirror toggle).</summary>
        public void ChooseHandedness(Handedness handedness) => ChosenHandedness = handedness;
    }
}
