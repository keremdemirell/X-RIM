using System;
using System.IO;
using NUnit.Framework;
using XRim.Economy;
using XRim.Profile;
using XRim.Rules;

namespace XRim.Tests.EditMode.Profile
{
    /// <summary>The profile file on disk (Session 18): safe to save, safe to lose power in the middle, and never destroying a player's progress.</summary>
    public sealed class ProfileStoreTests
    {
        private readonly XmlProfileTextCodec _codec = new XmlProfileTextCodec();
        private readonly ShopCatalogue _catalogue = ShopCatalogue.From(new EconomySettings());
        private string _folder;

        [SetUp]
        public void SetUp()
        {
            _folder = Path.Combine(Path.GetTempPath(), "XRimProfileTests-" + Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_folder)) Directory.Delete(_folder, true);
        }

        private ProfileStore NewStore(ProfileUpgrader upgrader = null) => new ProfileStore(_folder, _codec, _catalogue, upgrader);

        private PlayerProfile ProfileWith(long earned)
        {
            PlayerProfile profile = PlayerProfile.CreateNew(_catalogue);
            profile.Wallet.Add(new EarnedAmount(earned));
            return profile;
        }

        private long EarnedIn(string path) => _codec.FromText(File.ReadAllText(path)).Earned;

        [Test]
        public void NoFile_IsAFirstLaunch_AndNothingIsWrittenYet()
        {
            ProfileLoadResult result = NewStore().Load();

            Assert.That(result.Status, Is.EqualTo(ProfileLoadStatus.Created));
            Assert.That(result.Profile.HasChosenHandedness, Is.False);
            Assert.That(Directory.Exists(_folder), Is.False, "loading alone writes nothing");
        }

        [Test]
        public void SavedProfile_LoadsBackTheSame()
        {
            ProfileStore store = NewStore();
            PlayerProfile profile = ProfileWith(77);
            profile.ChooseHandedness(Handedness.Left);
            profile.Inventory.Grant(GameplayUnlockKind.Weapon, "spear");

            Assert.That(store.Save(profile), Is.True);
            ProfileLoadResult result = NewStore().Load();

            Assert.That(result.Status, Is.EqualTo(ProfileLoadStatus.Loaded));
            Assert.That(result.Profile.Wallet.Earned.Value, Is.EqualTo(77L));
            Assert.That(result.Profile.ChosenHandedness, Is.EqualTo(Handedness.Left));
            Assert.That(result.Profile.Inventory.Owns(GameplayUnlockKind.Weapon, "spear"), Is.True);
        }

        [Test]
        public void ASave_LeavesNoHalfWrittenFileBehind()
        {
            ProfileStore store = NewStore();

            store.Save(ProfileWith(1));
            store.Save(ProfileWith(2));

            Assert.That(File.Exists(store.FilePath + ProfileFormat.TempSuffix), Is.False);
            Assert.That(File.Exists(store.FilePath), Is.True);
        }

        [Test]
        public void EachSave_KeepsThePreviousFileAsTheBackup()
        {
            ProfileStore store = NewStore();

            store.Save(ProfileWith(10));
            store.Save(ProfileWith(20));

            Assert.That(EarnedIn(store.FilePath), Is.EqualTo(20L));
            Assert.That(EarnedIn(store.BackupPath), Is.EqualTo(10L));
        }

        [Test]
        public void ADamagedFile_WithNoBackup_StartsFresh_AndIsKeptAside()
        {
            ProfileStore store = NewStore();
            Directory.CreateDirectory(_folder);
            File.WriteAllText(store.FilePath, "this is not a profile");

            ProfileLoadResult result = store.Load();

            Assert.That(result.Status, Is.EqualTo(ProfileLoadStatus.CorruptStartedFresh));
            Assert.That(result.Message, Does.Contain("damaged"));
            Assert.That(result.Profile.Wallet.Earned.Value, Is.EqualTo(0L));
            Assert.That(File.Exists(store.FilePath), Is.False);
            Assert.That(File.ReadAllText(store.CorruptPath), Is.EqualTo("this is not a profile"), "the damaged file is kept, never deleted");
            Assert.That(store.CanSave, Is.True, "a damaged file is no reason to stop saving: the new profile starts clean");
        }

        [Test]
        public void ADamagedFile_WithABackup_RecoversFromTheBackup()
        {
            ProfileStore store = NewStore();
            store.Save(ProfileWith(10));
            store.Save(ProfileWith(20));
            File.WriteAllText(store.FilePath, "<broken");

            ProfileLoadResult result = NewStore().Load();

            Assert.That(result.Status, Is.EqualTo(ProfileLoadStatus.RecoveredFromBackup));
            Assert.That(result.Profile.Wallet.Earned.Value, Is.EqualTo(10L), "the last good save before the damage");
            Assert.That(File.ReadAllText(store.CorruptPath), Is.EqualTo("<broken"));
            Assert.That(EarnedIn(store.FilePath), Is.EqualTo(10L), "the recovered profile is written back as the real file");
        }

        [Test]
        public void AMissingFile_WithABackup_RecoversFromTheBackup()
        {
            ProfileStore store = NewStore();
            store.Save(ProfileWith(10));
            store.Save(ProfileWith(20));
            File.Delete(store.FilePath);

            ProfileLoadResult result = NewStore().Load();

            Assert.That(result.Status, Is.EqualTo(ProfileLoadStatus.RecoveredFromBackup));
            Assert.That(result.Profile.Wallet.Earned.Value, Is.EqualTo(10L));
        }

        [Test]
        public void AFileWithoutAVersion_IsTreatedAsDamaged()
        {
            ProfileStore store = NewStore();
            Directory.CreateDirectory(_folder);
            File.WriteAllText(store.FilePath, _codec.ToText(new ProfileData { Version = 0, Earned = 500 }));

            ProfileLoadResult result = store.Load();

            Assert.That(result.Status, Is.EqualTo(ProfileLoadStatus.CorruptStartedFresh));
            Assert.That(File.Exists(store.CorruptPath), Is.True, "it may be someone's file: kept, not deleted");
        }

        [Test]
        public void AFileFromANewerGame_IsLeftUntouched_AndNothingIsSavedOverIt()
        {
            ProfileStore store = NewStore();
            Directory.CreateDirectory(_folder);
            string newer = _codec.ToText(new ProfileData { Version = ProfileFormat.CurrentVersion + 5, Earned = 900 });
            File.WriteAllText(store.FilePath, newer);

            ProfileLoadResult result = store.Load();

            Assert.That(result.Status, Is.EqualTo(ProfileLoadStatus.NewerVersionReadOnly));
            Assert.That(store.CanSave, Is.False);
            Assert.That(store.Save(result.Profile), Is.False);
            Assert.That(store.LastSaveProblem, Is.Not.Empty);
            Assert.That(File.ReadAllText(store.FilePath), Is.EqualTo(newer), "installing an older build never destroys a newer profile");
            Assert.That(File.Exists(store.CorruptPath), Is.False, "it is not damaged, so it is not moved");
            Assert.That(File.Exists(store.BackupPath), Is.False);
        }

        [Test]
        public void AnOlderFile_IsUpgraded_SavedAgain_AndTheOldOneKeptAsTheBackup()
        {
            ProfileStore old = NewStore();
            Directory.CreateDirectory(_folder);
            File.WriteAllText(old.FilePath, _codec.ToText(new ProfileData { Version = 1, Earned = 5 }));
            var upgrader = new ProfileUpgrader(2, new IProfileUpgrade[] { new AddTen() });

            ProfileLoadResult result = NewStore(upgrader).Load();

            Assert.That(result.Status, Is.EqualTo(ProfileLoadStatus.Upgraded));
            Assert.That(result.Profile.Wallet.Earned.Value, Is.EqualTo(15L));
            Assert.That(_codec.FromText(File.ReadAllText(old.FilePath)).Version, Is.EqualTo(2), "the file on disk is now the new version");
            Assert.That(_codec.FromText(File.ReadAllText(old.BackupPath)).Version, Is.EqualTo(1), "the old file is kept");
            Assert.That(EarnedIn(old.BackupPath), Is.EqualTo(5L));
        }

        [Test]
        public void Delete_ErasesEveryProfileFile_AndTheNextLoadIsAFirstLaunch()
        {
            ProfileStore store = NewStore();
            store.Save(ProfileWith(10));
            store.Save(ProfileWith(20));
            File.WriteAllText(store.CorruptPath, "x");

            store.Delete();

            Assert.That(File.Exists(store.FilePath), Is.False);
            Assert.That(File.Exists(store.BackupPath), Is.False);
            Assert.That(File.Exists(store.CorruptPath), Is.False);
            Assert.That(NewStore().Load().Status, Is.EqualTo(ProfileLoadStatus.Created));
        }

        [Test]
        public void DeleteFiles_OnAFolderThatDoesNotExist_DoesNothing()
        {
            Assert.DoesNotThrow(() => ProfileStore.DeleteFiles(_folder));
        }

        [Test]
        public void ASaveTheDiskRefuses_ReturnsFalse_AndSaysWhy()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_folder));
            File.WriteAllText(_folder, "a file where the folder should be");
            ProfileStore store = NewStore();

            bool saved = store.Save(ProfileWith(1));

            Assert.That(saved, Is.False);
            Assert.That(store.LastSaveProblem, Does.Contain("could not be saved"));
            File.Delete(_folder);
        }

        [Test]
        public void ASave_DoesNotChangeThePlayersProfileInMemory()
        {
            ProfileStore store = NewStore();
            PlayerProfile profile = ProfileWith(33);

            store.Save(profile);

            Assert.That(profile.Wallet.Earned.Value, Is.EqualTo(33L));
        }

        private sealed class AddTen : IProfileUpgrade
        {
            public int FromVersion => 1;

            public ProfileData Upgrade(ProfileData old)
            {
                old.Earned += 10;
                return old;
            }
        }
    }
}
