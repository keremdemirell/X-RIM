namespace XRim.Profile
{
    /// <summary>What the saved profile file looks like on disk (Session 18).</summary>
    public static class ProfileFormat
    {
        /// <summary>
        /// The version the game writes. Raise it whenever <see cref="ProfileData"/> changes shape, and add the matching
        /// <see cref="IProfileUpgrade"/> from the old version: an older file must keep loading after an update.
        /// </summary>
        public const int CurrentVersion = 1;

        public const string FileName = "profile.json";

        /// <summary>The copy of the last good file, made each time the profile is saved over it.</summary>
        public const string BackupSuffix = ".bak";

        /// <summary>The new file while it is being written; it replaces the real one only when complete.</summary>
        public const string TempSuffix = ".tmp";

        /// <summary>A file that could not be read is kept under this name, never deleted.</summary>
        public const string CorruptSuffix = ".corrupt";
    }
}
