namespace XRim.Profile
{
    public enum ProfileUpgradeStatus
    {
        /// <summary>The file was written by this version of the game.</summary>
        UpToDate = 0,

        /// <summary>The file was older; every step up to the current version ran.</summary>
        Upgraded = 1,

        /// <summary>The file was written by a newer game. It must not be read as if it were ours, and must never be overwritten.</summary>
        NewerVersion = 2,

        /// <summary>The file has no usable version, or a step on the way is missing.</summary>
        Unsupported = 3,
    }
}
