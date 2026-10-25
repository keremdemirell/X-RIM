using System;
using System.Collections.Generic;
using XRim.Core;
using XRim.Economy;
using XRim.Rules;

namespace XRim.Profile
{
    /// <summary>
    /// Turns a <see cref="PlayerProfile"/> into the saved file's shape and back. Loading is forgiving, because a file can be edited, damaged
    /// or written by an earlier game: unknown kinds and empty ids are dropped, balances never go below 0, volumes stay inside 0..1, an
    /// equipped cosmetic the player does not own is unequipped, and the starting items are always owned (a player can never lose them).
    /// </summary>
    public static class ProfileCodec
    {
        public static ProfileData ToData(PlayerProfile profile)
        {
            Guard.NotNull(profile, nameof(profile));
            var data = new ProfileData
            {
                Version = ProfileFormat.CurrentVersion,
                HandednessChosen = profile.HasChosenHandedness,
                Handedness = profile.HasChosenHandedness ? (int)profile.ChosenHandedness.Value : 0,
                VolumeMaster = profile.Volumes.Master,
                VolumeMusic = profile.Volumes.Music,
                VolumeEffects = profile.Volumes.Effects,
                VolumeVoices = profile.Volumes.Voices,
                Earned = profile.Wallet.Earned.Value,
                Premium = profile.Wallet.Premium.Value,
            };

            foreach ((GameplayUnlockKind kind, string id) in profile.Inventory.OwnedUnlocks())
            {
                data.Unlocks.Add(new ProfileUnlockData((int)kind, id));
            }

            foreach ((CosmeticKind kind, string id) in profile.Inventory.OwnedCosmetics())
            {
                data.Cosmetics.Add(new ProfileCosmeticData((int)kind, id));
            }

            foreach (CosmeticKind kind in Enum.GetValues(typeof(CosmeticKind)))
            {
                string equipped = profile.Inventory.EquippedCosmetic(kind);
                if (equipped != null) data.Equipped.Add(new ProfileEquippedData((int)kind, equipped));
            }

            return data;
        }

        /// <param name="catalogue">What the shop starts a player with; those items are granted whatever the file says.</param>
        public static PlayerProfile FromData(ProfileData data, ShopCatalogue catalogue)
        {
            Guard.NotNull(data, nameof(data));
            Guard.NotNull(catalogue, nameof(catalogue));

            var wallet = new Wallet();
            wallet.Add(new EarnedAmount(Math.Max(data.Earned, 0L)));
            wallet.Add(new PremiumAmount(Math.Max(data.Premium, 0L)));

            var volumes = new AudioVolumes
            {
                Master = data.VolumeMaster,
                Music = data.VolumeMusic,
                Effects = data.VolumeEffects,
                Voices = data.VolumeVoices,
            };

            Handedness? handedness = null;
            if (data.HandednessChosen && Enum.IsDefined(typeof(Handedness), data.Handedness)) handedness = (Handedness)data.Handedness;

            var inventory = new Inventory();
            foreach (ProfileUnlockData unlock in data.Unlocks ?? new List<ProfileUnlockData>())
            {
                if (unlock != null && !string.IsNullOrEmpty(unlock.Id) && Enum.IsDefined(typeof(GameplayUnlockKind), unlock.Kind))
                {
                    inventory.Grant((GameplayUnlockKind)unlock.Kind, unlock.Id);
                }
            }

            foreach (ProfileCosmeticData cosmetic in data.Cosmetics ?? new List<ProfileCosmeticData>())
            {
                if (cosmetic != null && !string.IsNullOrEmpty(cosmetic.Id) && Enum.IsDefined(typeof(CosmeticKind), cosmetic.Kind))
                {
                    inventory.GrantCosmetic((CosmeticKind)cosmetic.Kind, cosmetic.Id);
                }
            }

            foreach (ProfileEquippedData equipped in data.Equipped ?? new List<ProfileEquippedData>())
            {
                if (equipped != null && Enum.IsDefined(typeof(CosmeticKind), equipped.Kind)) inventory.Equip((CosmeticKind)equipped.Kind, equipped.Id);
            }

            inventory.GrantStarting(catalogue);
            return new PlayerProfile(wallet, inventory, volumes, handedness);
        }
    }
}
