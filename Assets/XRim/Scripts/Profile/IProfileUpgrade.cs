namespace XRim.Profile
{
    /// <summary>
    /// One step in the chain that brings an old saved profile up to the current format: it takes the data as <see cref="FromVersion"/> wrote it
    /// and returns it as the next version reads it. A step never stamps the version; <see cref="ProfileUpgrader"/> does.
    /// </summary>
    public interface IProfileUpgrade
    {
        /// <summary>The version this step reads. It produces <c>FromVersion + 1</c>.</summary>
        int FromVersion { get; }

        ProfileData Upgrade(ProfileData old);
    }
}
