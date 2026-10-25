namespace XRim.Profile
{
    public sealed class ProfileLoadResult
    {
        public PlayerProfile Profile { get; }
        public ProfileLoadStatus Status { get; }

        /// <summary>One plain sentence for the log when something was not the normal case; empty otherwise.</summary>
        public string Message { get; }

        public ProfileLoadResult(PlayerProfile profile, ProfileLoadStatus status, string message = "")
        {
            Profile = profile;
            Status = status;
            Message = message ?? string.Empty;
        }
    }
}
