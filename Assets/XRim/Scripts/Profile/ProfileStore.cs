using System;
using System.IO;
using System.Text;
using XRim.Core;
using XRim.Economy;

namespace XRim.Profile
{
    /// <summary>
    /// Keeps the profile in one versioned file in a folder (the game gives it a folder under <c>Application.persistentDataPath</c>). It is
    /// built so that a player's progress is never lost by accident:
    /// <list type="bullet">
    /// <item>A save writes a new file first and swaps it in only when complete; the file it replaces becomes the backup.</item>
    /// <item>A damaged file is kept aside (<see cref="ProfileFormat.CorruptSuffix"/>), never deleted; the backup is tried next.</item>
    /// <item>An older file is brought up to date by <see cref="ProfileUpgrader"/> and saved again, the old one kept as the backup.</item>
    /// <item>A file from a newer game, or one that cannot be read at all, is left exactly as it is and nothing is saved over it.</item>
    /// </list>
    /// Engine-free: it only needs a folder and an <see cref="IProfileTextCodec"/>.
    /// </summary>
    public sealed class ProfileStore
    {
        private static readonly UTF8Encoding Utf8WithoutByteOrderMark = new UTF8Encoding(false);

        private readonly string _directory;
        private readonly IProfileTextCodec _codec;
        private readonly ShopCatalogue _catalogue;
        private readonly ProfileUpgrader _upgrader;

        public ProfileStore(string directory, IProfileTextCodec codec, ShopCatalogue catalogue, ProfileUpgrader upgrader = null)
        {
            _directory = Guard.NotNull(directory, nameof(directory));
            _codec = Guard.NotNull(codec, nameof(codec));
            _catalogue = Guard.NotNull(catalogue, nameof(catalogue));
            _upgrader = upgrader ?? ProfileUpgrader.Default();
        }

        public string DirectoryPath => _directory;
        public string FilePath => Path.Combine(_directory, ProfileFormat.FileName);
        public string BackupPath => FilePath + ProfileFormat.BackupSuffix;
        public string CorruptPath => FilePath + ProfileFormat.CorruptSuffix;
        private string TempPath => FilePath + ProfileFormat.TempSuffix;

        /// <summary>False after a load that found a newer or unreadable file: <see cref="Save"/> then does nothing, so that file survives.</summary>
        public bool CanSave { get; private set; } = true;

        /// <summary>Why the last <see cref="Save"/> did nothing or failed; empty after a good save.</summary>
        public string LastSaveProblem { get; private set; } = string.Empty;

        public ProfileLoadResult Load()
        {
            CanSave = true;
            bool mainWasDamaged = false;
            string damage = string.Empty;

            if (File.Exists(FilePath))
            {
                ReadOutcome main = Read(FilePath);
                switch (main.Kind)
                {
                    case ReadKind.Ok when main.WasUpgraded:
                        Save(main.Profile);
                        return new ProfileLoadResult(main.Profile, ProfileLoadStatus.Upgraded,
                            $"The profile was saved by an older game (version {main.FoundVersion}) and has been updated; the old file is kept as {ProfileFormat.FileName}{ProfileFormat.BackupSuffix}.");
                    case ReadKind.Ok:
                        return new ProfileLoadResult(main.Profile, ProfileLoadStatus.Loaded);
                    case ReadKind.Newer:
                        CanSave = false;
                        return Fresh(ProfileLoadStatus.NewerVersionReadOnly,
                            $"The profile file was saved by a newer game (version {main.FoundVersion}). It is left untouched and nothing will be saved this run.");
                    case ReadKind.Unreadable:
                        CanSave = false;
                        return Fresh(ProfileLoadStatus.UnreadableReadOnly,
                            $"The profile file could not be read ({main.Problem}). It is left untouched and nothing will be saved this run.");
                    default:
                        mainWasDamaged = true;
                        damage = main.Problem;
                        MoveDamagedFileAside();
                        break;
                }
            }

            if (File.Exists(BackupPath))
            {
                ReadOutcome backup = Read(BackupPath);
                if (backup.Kind == ReadKind.Ok)
                {
                    Save(backup.Profile);
                    return new ProfileLoadResult(backup.Profile, ProfileLoadStatus.RecoveredFromBackup, mainWasDamaged
                        ? $"The profile file was damaged ({damage}); the last backup was used and the damaged file kept as {ProfileFormat.FileName}{ProfileFormat.CorruptSuffix}."
                        : "The profile file was missing; the last backup was used.");
                }
            }

            return mainWasDamaged
                ? Fresh(ProfileLoadStatus.CorruptStartedFresh,
                    $"The profile file was damaged ({damage}) and there was no usable backup: starting a fresh profile; the damaged file is kept as {ProfileFormat.FileName}{ProfileFormat.CorruptSuffix}.")
                : Fresh(ProfileLoadStatus.Created);
        }

        /// <summary>Writes the profile. False when saving is switched off (see <see cref="CanSave"/>) or the disk refused; <see cref="LastSaveProblem"/> says why.</summary>
        public bool Save(PlayerProfile profile)
        {
            Guard.NotNull(profile, nameof(profile));
            if (!CanSave)
            {
                LastSaveProblem = "Saving is off because the profile file on disk was left untouched (newer version or unreadable).";
                return false;
            }

            try
            {
                System.IO.Directory.CreateDirectory(_directory);
                ProfileData data = ProfileCodec.ToData(profile);
                data.Version = _upgrader.CurrentVersion; // the version the store reads is the version it writes
                File.WriteAllText(TempPath, _codec.ToText(data), Utf8WithoutByteOrderMark);
                if (File.Exists(FilePath)) ReplaceFile();
                else File.Move(TempPath, FilePath);
                LastSaveProblem = string.Empty;
                return true;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                LastSaveProblem = "The profile could not be saved: " + exception.Message;
                return false;
            }
        }

        /// <summary>Erases the saved profile (and its backup and kept-aside copies): the next <see cref="Load"/> is a first launch.</summary>
        public void Delete()
        {
            DeleteFiles(_directory);
            CanSave = true;
        }

        /// <summary>Erases every profile file in a folder. The Editor's Reset Profile menu uses it without building a store.</summary>
        public static void DeleteFiles(string directory)
        {
            Guard.NotNull(directory, nameof(directory));
            string main = Path.Combine(directory, ProfileFormat.FileName);
            foreach (string path in new[] { main, main + ProfileFormat.BackupSuffix, main + ProfileFormat.TempSuffix, main + ProfileFormat.CorruptSuffix })
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        private void ReplaceFile()
        {
            try
            {
                File.Replace(TempPath, FilePath, BackupPath);
            }
            catch (Exception exception) when (exception is IOException || exception is PlatformNotSupportedException || exception is UnauthorizedAccessException)
            {
                // Some platforms cannot swap in one step: keep the backup by hand, then move the new file into place.
                File.Copy(FilePath, BackupPath, true);
                File.Delete(FilePath);
                File.Move(TempPath, FilePath);
            }
        }

        private ProfileLoadResult Fresh(ProfileLoadStatus status, string message = "") =>
            new ProfileLoadResult(PlayerProfile.CreateNew(_catalogue), status, message);

        private void MoveDamagedFileAside()
        {
            try
            {
                if (File.Exists(CorruptPath)) File.Delete(CorruptPath);
                File.Move(FilePath, CorruptPath);
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                // If it cannot be moved it stays where it is; the next save replaces it, and its text is still in the backup chain.
                LastSaveProblem = "The damaged profile file could not be moved aside: " + exception.Message;
            }
        }

        private ReadOutcome Read(string path)
        {
            string text;
            try
            {
                text = File.ReadAllText(path, Utf8WithoutByteOrderMark);
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                return ReadOutcome.Unreadable(exception.Message);
            }

            ProfileData data;
            try
            {
                data = _codec.FromText(text);
            }
            catch (FormatException exception)
            {
                return ReadOutcome.Damaged(exception.Message);
            }

            ProfileUpgradeResult upgrade = _upgrader.Upgrade(data);
            switch (upgrade.Status)
            {
                case ProfileUpgradeStatus.NewerVersion:
                    return ReadOutcome.Newer(upgrade.FoundVersion);
                case ProfileUpgradeStatus.Unsupported:
                    return ReadOutcome.Damaged($"it has no usable version number ({upgrade.FoundVersion})");
                default:
                    return ReadOutcome.Ok(ProfileCodec.FromData(upgrade.Data, _catalogue), upgrade.Status == ProfileUpgradeStatus.Upgraded, upgrade.FoundVersion);
            }
        }

        private enum ReadKind
        {
            Ok,
            Damaged,
            Newer,
            Unreadable,
        }

        private sealed class ReadOutcome
        {
            public ReadKind Kind { get; private set; }
            public PlayerProfile Profile { get; private set; }
            public bool WasUpgraded { get; private set; }
            public int FoundVersion { get; private set; }
            public string Problem { get; private set; } = string.Empty;

            public static ReadOutcome Ok(PlayerProfile profile, bool upgraded, int foundVersion) =>
                new ReadOutcome { Kind = ReadKind.Ok, Profile = profile, WasUpgraded = upgraded, FoundVersion = foundVersion };

            public static ReadOutcome Damaged(string problem) => new ReadOutcome { Kind = ReadKind.Damaged, Problem = problem };

            public static ReadOutcome Newer(int foundVersion) => new ReadOutcome { Kind = ReadKind.Newer, FoundVersion = foundVersion };

            public static ReadOutcome Unreadable(string problem) => new ReadOutcome { Kind = ReadKind.Unreadable, Problem = problem };
        }
    }
}
