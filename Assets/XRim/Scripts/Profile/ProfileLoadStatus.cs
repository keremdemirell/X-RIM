namespace XRim.Profile
{
    /// <summary>How <see cref="ProfileStore.Load"/> got the profile.</summary>
    public enum ProfileLoadStatus
    {
        /// <summary>No file: a first launch. Nothing is written until the first save.</summary>
        Created = 0,

        Loaded = 1,

        /// <summary>An older file was brought up to date and saved again (the old one is kept as the backup).</summary>
        Upgraded = 2,

        /// <summary>The file was damaged; the last good backup was used and the damaged file was kept aside.</summary>
        RecoveredFromBackup = 3,

        /// <summary>The file was damaged and there was no usable backup: a fresh profile; the damaged file was kept aside.</summary>
        CorruptStartedFresh = 4,

        /// <summary>
        /// The file was written by a newer game. A fresh profile is used for this run, the file is left exactly as it is, and nothing is
        /// saved, so installing an older build never destroys a newer profile.
        /// </summary>
        NewerVersionReadOnly = 5,

        /// <summary>The file exists but the disk would not let it be read (locked, no permission). Treated like a newer file: left alone, nothing saved.</summary>
        UnreadableReadOnly = 6,
    }
}
