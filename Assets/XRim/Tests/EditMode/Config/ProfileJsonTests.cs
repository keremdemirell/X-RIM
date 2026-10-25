using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using XRim.Config;
using XRim.Economy;
using XRim.Profile;
using XRim.Rules;

namespace XRim.Tests.EditMode.Config
{
    /// <summary>The saved profile as JSON through Unity's JsonUtility (Session 18). These run in the Editor: JsonUtility needs the engine.</summary>
    public sealed class ProfileJsonTests
    {
        private readonly ProfileJson _json = new ProfileJson();
        private readonly ShopCatalogue _catalogue = ShopCatalogue.From(new EconomySettings());

        private PlayerProfile RichProfile()
        {
            PlayerProfile profile = PlayerProfile.CreateNew(_catalogue);
            profile.ChooseHandedness(Handedness.Left);
            profile.Wallet.Add(new EarnedAmount(1234));
            profile.Wallet.Add(new PremiumAmount(56));
            profile.Inventory.Grant(GameplayUnlockKind.Weapon, "sword");
            profile.Inventory.Grant(GameplayUnlockKind.SignatureMove, SignatureMoveIds.WhiplashCalibrationSpin.Value);
            profile.Inventory.GrantCosmetic(CosmeticKind.HitEffect, CosmeticIds.NeonSparks);
            profile.Inventory.Equip(CosmeticKind.HitEffect, CosmeticIds.NeonSparks);
            profile.Volumes.Master = 0.6f;
            profile.Volumes.Voices = 0.1f;
            return profile;
        }

        [Test]
        public void AProfile_RoundTripsThroughJsonText()
        {
            PlayerProfile profile = RichProfile();

            string text = _json.ToText(ProfileCodec.ToData(profile));
            PlayerProfile back = ProfileCodec.FromData(_json.FromText(text), _catalogue);

            Assert.That(back.ChosenHandedness, Is.EqualTo(Handedness.Left));
            Assert.That(back.Wallet.Earned.Value, Is.EqualTo(1234L));
            Assert.That(back.Wallet.Premium.Value, Is.EqualTo(56L));
            Assert.That(back.Inventory.OwnedUnlocks(), Is.EqualTo(profile.Inventory.OwnedUnlocks()));
            Assert.That(back.Inventory.OwnedCosmetics(), Is.EqualTo(profile.Inventory.OwnedCosmetics()));
            Assert.That(back.Inventory.EquippedCosmetic(CosmeticKind.HitEffect), Is.EqualTo(CosmeticIds.NeonSparks));
            Assert.That(back.Volumes.Master, Is.EqualTo(0.6f).Within(1e-6f));
            Assert.That(back.Volumes.Voices, Is.EqualTo(0.1f).Within(1e-6f));
        }

        [Test]
        public void TheText_IsReadableJsonWithTheVersionInIt()
        {
            string text = _json.ToText(ProfileCodec.ToData(RichProfile()));

            Assert.That(text, Does.Contain("\"Version\": " + ProfileFormat.CurrentVersion));
            Assert.That(text, Does.Contain("\"Earned\": 1234"));
            Assert.That(text, Does.Contain("\n"), "pretty-printed, so a person can read it");
        }

        [Test]
        public void EmptyAndBrokenText_AreAFormatProblem_NotACrash()
        {
            Assert.Throws<FormatException>(() => _json.FromText(string.Empty));
            Assert.Throws<FormatException>(() => _json.FromText("   "));
            Assert.Throws<FormatException>(() => _json.FromText("{ not json"));
            Assert.Throws<FormatException>(() => _json.FromText("null"));
        }

        [Test]
        public void AFileWithoutAVersion_ReadsAsVersionZero()
        {
            ProfileData data = _json.FromText("{\"Earned\": 40}");

            Assert.That(data.Version, Is.EqualTo(0));
            Assert.That(ProfileUpgrader.Default().Upgrade(data).Status, Is.EqualTo(ProfileUpgradeStatus.Unsupported));
        }

        [Test]
        public void FieldsTheFileLacks_KeepTheirStartingValues_AndUnknownFieldsAreIgnored()
        {
            ProfileData data = _json.FromText("{\"Version\": 1, \"Earned\": 40, \"SomethingFromTheFuture\": [1, 2, 3]}");

            Assert.That(data.Earned, Is.EqualTo(40L));
            Assert.That(data.VolumeMaster, Is.EqualTo(AudioVolumes.AsMixed), "a missing volume is the designer's mix, not silence");
            Assert.That(data.Unlocks, Is.Empty);
        }

        [Test]
        public void TheWholeStore_WorksWithTheRealJson()
        {
            string folder = Path.Combine(Path.GetTempPath(), "XRimProfileJsonTests-" + Guid.NewGuid().ToString("N"));
            try
            {
                var store = new ProfileStore(folder, _json, _catalogue);
                PlayerProfile profile = RichProfile();
                Assert.That(store.Save(profile), Is.True, store.LastSaveProblem);

                ProfileLoadResult loaded = new ProfileStore(folder, _json, _catalogue).Load();

                Assert.That(loaded.Status, Is.EqualTo(ProfileLoadStatus.Loaded), loaded.Message);
                Assert.That(loaded.Profile.Wallet.Earned.Value, Is.EqualTo(1234L));
                Assert.That(loaded.Profile.ChosenHandedness, Is.EqualTo(Handedness.Left));

                File.WriteAllText(store.FilePath, "{ damaged");
                Assert.That(new ProfileStore(folder, _json, _catalogue).Load().Status, Is.EqualTo(ProfileLoadStatus.CorruptStartedFresh).Or.EqualTo(ProfileLoadStatus.RecoveredFromBackup));
            }
            finally
            {
                if (Directory.Exists(folder)) Directory.Delete(folder, true);
            }
        }

        [Test]
        public void TheProfileFolder_IsUnderThePersistentDataPath()
        {
            string folder = ProfileLocation.DefaultDirectory();

            Assert.That(folder, Does.StartWith(Application.persistentDataPath));
            Assert.That(folder, Does.EndWith(ProfileLocation.FolderName));
        }
    }
}
