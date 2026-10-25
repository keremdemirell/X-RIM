using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using XRim.Economy;
using XRim.Profile;
using XRim.Rules;

namespace XRim.Tests.EditMode.Profile
{
    /// <summary>The profile as a saved file's data and back (Session 18): complete, and forgiving about a file that is odd.</summary>
    public sealed class ProfileCodecTests
    {
        private static ShopCatalogue Catalogue() => ShopCatalogue.From(new EconomySettings());

        [Test]
        public void NewProfile_HasTheStartingItemsEmptyWalletsAndNoHandedness()
        {
            PlayerProfile profile = PlayerProfile.CreateNew(Catalogue());

            Assert.That(profile.HasChosenHandedness, Is.False, "the first-launch question has not been answered");
            Assert.That(profile.Wallet.Earned.Value, Is.EqualTo(0L));
            Assert.That(profile.Wallet.Premium.Value, Is.EqualTo(0L));
            Assert.That(profile.Inventory.OwnedUnlocks().Select(item => item.Id),
                Is.EquivalentTo(new[] { "rapier", "mace", "shield", SignatureMoveIds.FiveFingerExplodingTouch.Value }));
            Assert.That(profile.Volumes.Master, Is.EqualTo(AudioVolumes.AsMixed));
        }

        [Test]
        public void RoundTrip_KeepsEverything()
        {
            ShopCatalogue catalogue = Catalogue();
            PlayerProfile profile = PlayerProfile.CreateNew(catalogue);
            profile.ChooseHandedness(Handedness.Left);
            profile.Wallet.Add(new EarnedAmount(120));
            profile.Wallet.Add(new PremiumAmount(40));
            profile.Inventory.Grant(GameplayUnlockKind.Weapon, "sword");
            profile.Inventory.GrantCosmetic(CosmeticKind.HitEffect, CosmeticIds.NeonSparks);
            profile.Inventory.GrantCosmetic(CosmeticKind.Skin, CosmeticIds.HazardStripes);
            profile.Inventory.Equip(CosmeticKind.HitEffect, CosmeticIds.NeonSparks);
            profile.Volumes.Master = 0.8f;
            profile.Volumes.Music = 0.25f;
            profile.Volumes.Effects = 0.5f;
            profile.Volumes.Voices = 0f;

            PlayerProfile back = ProfileCodec.FromData(ProfileCodec.ToData(profile), catalogue);

            Assert.That(back.ChosenHandedness, Is.EqualTo(Handedness.Left));
            Assert.That(back.Wallet.Earned.Value, Is.EqualTo(120L));
            Assert.That(back.Wallet.Premium.Value, Is.EqualTo(40L));
            Assert.That(back.Inventory.OwnedUnlocks(), Is.EqualTo(profile.Inventory.OwnedUnlocks()));
            Assert.That(back.Inventory.OwnedCosmetics(), Is.EqualTo(profile.Inventory.OwnedCosmetics()));
            Assert.That(back.Inventory.EquippedCosmetic(CosmeticKind.HitEffect), Is.EqualTo(CosmeticIds.NeonSparks));
            Assert.That(back.Inventory.EquippedCosmetic(CosmeticKind.Skin), Is.Null, "owned but not equipped stays that way");
            Assert.That(back.Volumes.Master, Is.EqualTo(0.8f));
            Assert.That(back.Volumes.Music, Is.EqualTo(0.25f));
            Assert.That(back.Volumes.Effects, Is.EqualTo(0.5f));
            Assert.That(back.Volumes.Voices, Is.EqualTo(0f));
        }

        [Test]
        public void ToData_StampsTheCurrentVersion()
        {
            Assert.That(ProfileCodec.ToData(PlayerProfile.CreateNew(Catalogue())).Version, Is.EqualTo(ProfileFormat.CurrentVersion));
        }

        [Test]
        public void ToData_AnUnpickedHandednessIsSavedAsUnpicked()
        {
            ProfileData data = ProfileCodec.ToData(PlayerProfile.CreateNew(Catalogue()));

            Assert.That(data.HandednessChosen, Is.False);
            Assert.That(ProfileCodec.FromData(data, Catalogue()).HasChosenHandedness, Is.False);
        }

        [Test]
        public void FromData_AlwaysGrantsTheStartingItems()
        {
            var data = new ProfileData { Version = 1 };

            PlayerProfile profile = ProfileCodec.FromData(data, Catalogue());

            Assert.That(profile.Inventory.Owns(GameplayUnlockKind.Weapon, "rapier"), Is.True);
            Assert.That(profile.Inventory.Owns(GameplayUnlockKind.Shield, "shield"), Is.True);
            Assert.That(profile.Inventory.Owns(GameplayUnlockKind.SignatureMove, SignatureMoveIds.FiveFingerExplodingTouch.Value), Is.True);
            Assert.That(profile.Inventory.Owns(GameplayUnlockKind.Weapon, "sword"), Is.False, "only what was bought, or is a starting item");
        }

        [Test]
        public void FromData_DropsWhatItCannotUnderstand()
        {
            var data = new ProfileData
            {
                Version = 1,
                Unlocks = new List<ProfileUnlockData>
                {
                    new ProfileUnlockData(99, "from_the_future"),
                    new ProfileUnlockData((int)GameplayUnlockKind.Weapon, string.Empty),
                    null,
                    new ProfileUnlockData((int)GameplayUnlockKind.Weapon, "sword"),
                },
                Cosmetics = new List<ProfileCosmeticData>
                {
                    new ProfileCosmeticData(-3, "bad_kind"),
                    new ProfileCosmeticData((int)CosmeticKind.Skin, null),
                    new ProfileCosmeticData((int)CosmeticKind.Skin, "hazard_stripes"),
                },
            };

            PlayerProfile profile = ProfileCodec.FromData(data, Catalogue());

            Assert.That(profile.Inventory.Owns(GameplayUnlockKind.Weapon, "sword"), Is.True);
            Assert.That(profile.Inventory.OwnedUnlocks().Count(), Is.EqualTo(5), "four starting items and the sword; nothing else got in");
            Assert.That(profile.Inventory.OwnedCosmetics().Select(item => item.Id), Is.EqualTo(new[] { "hazard_stripes" }));
        }

        [Test]
        public void FromData_NullListsAreTreatedAsEmpty()
        {
            var data = new ProfileData { Version = 1, Unlocks = null, Cosmetics = null, Equipped = null };

            PlayerProfile profile = ProfileCodec.FromData(data, Catalogue());

            Assert.That(profile.Inventory.OwnedUnlocks().Count(), Is.EqualTo(4));
            Assert.That(profile.Inventory.OwnedCosmetics(), Is.Empty);
        }

        [Test]
        public void FromData_NegativeBalancesBecomeZero()
        {
            PlayerProfile profile = ProfileCodec.FromData(new ProfileData { Version = 1, Earned = -50, Premium = -1 }, Catalogue());

            Assert.That(profile.Wallet.Earned.Value, Is.EqualTo(0L));
            Assert.That(profile.Wallet.Premium.Value, Is.EqualTo(0L));
        }

        [Test]
        public void FromData_VolumesStayInsideZeroToOne()
        {
            PlayerProfile profile = ProfileCodec.FromData(new ProfileData
            {
                Version = 1,
                VolumeMaster = 2f,
                VolumeMusic = -1f,
                VolumeEffects = float.NaN,
                VolumeVoices = 0.3f,
            }, Catalogue());

            Assert.That(profile.Volumes.Master, Is.EqualTo(1f));
            Assert.That(profile.Volumes.Music, Is.EqualTo(0f));
            Assert.That(profile.Volumes.Effects, Is.EqualTo(AudioVolumes.AsMixed), "a broken number becomes the designer's mix");
            Assert.That(profile.Volumes.Voices, Is.EqualTo(0.3f));
        }

        [Test]
        public void FromData_AHandednessNumberThatMeansNothingIsNotAPick()
        {
            PlayerProfile profile = ProfileCodec.FromData(new ProfileData { Version = 1, HandednessChosen = true, Handedness = 7 }, Catalogue());

            Assert.That(profile.HasChosenHandedness, Is.False, "the player is asked again rather than guessed");
        }

        [Test]
        public void FromData_AnEquippedCosmeticThatIsNotOwnedIsNotEquipped()
        {
            var data = new ProfileData
            {
                Version = 1,
                Equipped = new List<ProfileEquippedData> { new ProfileEquippedData((int)CosmeticKind.HitEffect, CosmeticIds.NeonSparks) },
            };

            PlayerProfile profile = ProfileCodec.FromData(data, Catalogue());

            Assert.That(profile.Inventory.EquippedCosmetic(CosmeticKind.HitEffect), Is.Null);
        }
    }
}
