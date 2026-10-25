using System;
using NUnit.Framework;
using XRim.Profile;

namespace XRim.Tests.EditMode.Profile
{
    /// <summary>
    /// The version upgrade (Session 18). The game's format is version 1, the first, so there is no real older file yet: the chain is tested
    /// with made-up steps, so the day the format changes an old player's file is already known to keep loading.
    /// </summary>
    public sealed class ProfileUpgraderTests
    {
        [Test]
        public void TheGamesOwnUpgrader_ReadsTheCurrentVersionAsIs()
        {
            var data = new ProfileData { Version = ProfileFormat.CurrentVersion, Earned = 9 };

            ProfileUpgradeResult result = ProfileUpgrader.Default().Upgrade(data);

            Assert.That(result.Status, Is.EqualTo(ProfileUpgradeStatus.UpToDate));
            Assert.That(result.Data, Is.SameAs(data));
            Assert.That(result.IsUsable, Is.True);
        }

        [Test]
        public void AFileWithoutAVersion_IsUnsupported()
        {
            ProfileUpgradeResult result = ProfileUpgrader.Default().Upgrade(new ProfileData { Version = 0 });

            Assert.That(result.Status, Is.EqualTo(ProfileUpgradeStatus.Unsupported));
            Assert.That(result.IsUsable, Is.False);
        }

        [Test]
        public void AFileFromANewerGame_IsNotReadAsOurs()
        {
            ProfileUpgradeResult result = ProfileUpgrader.Default().Upgrade(new ProfileData { Version = ProfileFormat.CurrentVersion + 1 });

            Assert.That(result.Status, Is.EqualTo(ProfileUpgradeStatus.NewerVersion));
            Assert.That(result.FoundVersion, Is.EqualTo(ProfileFormat.CurrentVersion + 1));
            Assert.That(result.IsUsable, Is.False);
        }

        [Test]
        public void Steps_RunInOrder_AndTheUpgraderStampsTheVersion()
        {
            var upgrader = new ProfileUpgrader(3, new IProfileUpgrade[] { new AddEarned(1, 10), new DoubleEarned(2) });

            ProfileUpgradeResult result = upgrader.Upgrade(new ProfileData { Version = 1, Earned = 5 });

            Assert.That(result.Status, Is.EqualTo(ProfileUpgradeStatus.Upgraded));
            Assert.That(result.Data.Earned, Is.EqualTo(30L), "(5 + 10) * 2: version 1's step runs before version 2's");
            Assert.That(result.Data.Version, Is.EqualTo(3));
            Assert.That(result.FoundVersion, Is.EqualTo(1));
        }

        [Test]
        public void AFileAlreadyPartWayUp_OnlyRunsTheLaterSteps()
        {
            var upgrader = new ProfileUpgrader(3, new IProfileUpgrade[] { new AddEarned(1, 10), new DoubleEarned(2) });

            ProfileUpgradeResult result = upgrader.Upgrade(new ProfileData { Version = 2, Earned = 5 });

            Assert.That(result.Data.Earned, Is.EqualTo(10L));
            Assert.That(result.Data.Version, Is.EqualTo(3));
        }

        [Test]
        public void AMissingStepOnTheWay_IsUnsupported_NotGuessed()
        {
            var upgrader = new ProfileUpgrader(3, new IProfileUpgrade[] { new AddEarned(1, 10) });

            ProfileUpgradeResult result = upgrader.Upgrade(new ProfileData { Version = 1, Earned = 5 });

            Assert.That(result.Status, Is.EqualTo(ProfileUpgradeStatus.Unsupported));
        }

        [Test]
        public void AStepThatReturnsNothing_IsABug_NotASilentLoss()
        {
            var upgrader = new ProfileUpgrader(2, new IProfileUpgrade[] { new ReturnsNothing() });

            Assert.Throws<InvalidOperationException>(() => upgrader.Upgrade(new ProfileData { Version = 1 }));
        }

        private sealed class AddEarned : IProfileUpgrade
        {
            private readonly long _amount;

            public AddEarned(int fromVersion, long amount)
            {
                FromVersion = fromVersion;
                _amount = amount;
            }

            public int FromVersion { get; }

            public ProfileData Upgrade(ProfileData old)
            {
                old.Earned += _amount;
                return old;
            }
        }

        private sealed class DoubleEarned : IProfileUpgrade
        {
            public DoubleEarned(int fromVersion)
            {
                FromVersion = fromVersion;
            }

            public int FromVersion { get; }

            public ProfileData Upgrade(ProfileData old)
            {
                old.Earned *= 2;
                return old;
            }
        }

        private sealed class ReturnsNothing : IProfileUpgrade
        {
            public int FromVersion => 1;

            public ProfileData Upgrade(ProfileData old) => null;
        }
    }
}
