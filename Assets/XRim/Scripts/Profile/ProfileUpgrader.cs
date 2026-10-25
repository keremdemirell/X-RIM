using System.Collections.Generic;
using XRim.Core;

namespace XRim.Profile
{
    /// <summary>
    /// Brings a saved profile up to the current format by running <see cref="IProfileUpgrade"/> steps one version at a time. The game's
    /// own upgrader (<see cref="Default"/>) has no steps yet because format 1 is the first; the day the format changes, the old version
    /// gets a step and an old player's file keeps loading.
    /// </summary>
    public sealed class ProfileUpgrader
    {
        private readonly Dictionary<int, IProfileUpgrade> _steps = new Dictionary<int, IProfileUpgrade>();

        public int CurrentVersion { get; }

        public ProfileUpgrader(int currentVersion, IEnumerable<IProfileUpgrade> steps)
        {
            CurrentVersion = Guard.Positive(currentVersion, nameof(currentVersion));
            foreach (IProfileUpgrade step in Guard.NotNull(steps, nameof(steps)))
            {
                _steps[step.FromVersion] = step;
            }
        }

        /// <summary>The upgrader for the format the game writes now.</summary>
        public static ProfileUpgrader Default() => new ProfileUpgrader(ProfileFormat.CurrentVersion, new IProfileUpgrade[0]);

        public ProfileUpgradeResult Upgrade(ProfileData data)
        {
            Guard.NotNull(data, nameof(data));
            int found = data.Version;
            if (found > CurrentVersion) return new ProfileUpgradeResult(ProfileUpgradeStatus.NewerVersion, data, found);
            if (found < 1) return new ProfileUpgradeResult(ProfileUpgradeStatus.Unsupported, data, found);
            if (found == CurrentVersion) return new ProfileUpgradeResult(ProfileUpgradeStatus.UpToDate, data, found);

            ProfileData current = data;
            for (int version = found; version < CurrentVersion; version++)
            {
                if (!_steps.TryGetValue(version, out IProfileUpgrade step)) return new ProfileUpgradeResult(ProfileUpgradeStatus.Unsupported, data, found);
                current = step.Upgrade(current) ?? throw new System.InvalidOperationException($"The upgrade from profile version {version} returned nothing.");
                current.Version = version + 1;
            }

            return new ProfileUpgradeResult(ProfileUpgradeStatus.Upgraded, current, found);
        }
    }
}
