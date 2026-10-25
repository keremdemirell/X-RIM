namespace XRim.Profile
{
    public readonly struct ProfileUpgradeResult
    {
        public ProfileUpgradeStatus Status { get; }

        /// <summary>The data at the current version for <see cref="ProfileUpgradeStatus.UpToDate"/> and <see cref="ProfileUpgradeStatus.Upgraded"/>; otherwise as it came in.</summary>
        public ProfileData Data { get; }

        /// <summary>The version the file said it was.</summary>
        public int FoundVersion { get; }

        public ProfileUpgradeResult(ProfileUpgradeStatus status, ProfileData data, int foundVersion)
        {
            Status = status;
            Data = data;
            FoundVersion = foundVersion;
        }

        public bool IsUsable => Status == ProfileUpgradeStatus.UpToDate || Status == ProfileUpgradeStatus.Upgraded;
    }
}
