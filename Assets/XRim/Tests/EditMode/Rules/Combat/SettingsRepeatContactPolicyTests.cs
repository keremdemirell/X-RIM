using NUnit.Framework;
using XRim.Rules;
using XRim.Rules.Combat;
using XRim.Rules.Settings;

namespace XRim.Tests.EditMode.Rules.Combat
{
    /// <summary>Repeat contacts between the same two held items in one turn (GDD §10 open point, D19).</summary>
    public sealed class SettingsRepeatContactPolicyTests
    {
        private readonly SettingsRepeatContactPolicy _policy = new SettingsRepeatContactPolicy();
        private ClashSettings _settings;

        [SetUp]
        public void SetUp() => _settings = new ClashSettings();

        [Test]
        public void D19Default_OnlyTheFirstContactResolves()
        {
            Assert.That(new RulePolicies().RepeatContact, Is.InstanceOf<SettingsRepeatContactPolicy>());
            Assert.That(_settings.MaxResolvedContactsPerWeaponPair, Is.EqualTo(1));

            Assert.That(_policy.ShouldResolve(0, _settings), Is.True);
            Assert.That(_policy.ShouldResolve(1, _settings), Is.False);
            Assert.That(_policy.ShouldResolve(5, _settings), Is.False);
        }

        [Test]
        public void ALimitOfTwo_ResolvesTheFirstTwo()
        {
            _settings.MaxResolvedContactsPerWeaponPair = 2;

            Assert.That(_policy.ShouldResolve(1, _settings), Is.True);
            Assert.That(_policy.ShouldResolve(2, _settings), Is.False);
        }

        [Test]
        public void ALimitOfZero_ResolvesEveryContact()
        {
            _settings.MaxResolvedContactsPerWeaponPair = 0;

            Assert.That(_policy.ShouldResolve(0, _settings), Is.True);
            Assert.That(_policy.ShouldResolve(100, _settings), Is.True);
        }
    }
}
