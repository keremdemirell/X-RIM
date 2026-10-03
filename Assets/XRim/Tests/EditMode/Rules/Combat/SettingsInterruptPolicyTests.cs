using NUnit.Framework;
using XRim.Core;
using XRim.Rules;
using XRim.Rules.Combat;
using XRim.Rules.Damage;
using XRim.Rules.Settings;

namespace XRim.Tests.EditMode.Rules.Combat
{
    /// <summary>Every option of the default interrupt policy (GDD §9; D15 options, D16 swing armour).</summary>
    public sealed class SettingsInterruptPolicyTests
    {
        private readonly SettingsInterruptPolicy _policy = new SettingsInterruptPolicy();
        private DamageSettings _settings;
        private WeaponStats _mace;

        [SetUp]
        public void SetUp()
        {
            _settings = new DamageSettings();
            _mace = GddStartingValues.Mace();
        }

        private bool Interrupts(BodyPart part, float damage, bool weaponArm = false) =>
            _policy.Interrupts(new InterruptCheck(new HitFacts(Side.Left, part, WeaponIds.Rapier, SimTime.FromMilliseconds(100), false),
                new DamageResult(damage, damage, 0f, false, false), _mace, weaponArm, _settings));

        [Test]
        public void AnyHit_Interrupts()
        {
            _settings.InterruptRule = InterruptRule.AnyHit;

            Assert.That(Interrupts(BodyPart.LeftLeg, 1f), Is.True);
        }

        [Test]
        public void WeaponArmOrHead_InterruptsOnlyThere()
        {
            _settings.InterruptRule = InterruptRule.WeaponArmOrHead;

            Assert.That(Interrupts(BodyPart.Head, 1f), Is.True);
            Assert.That(Interrupts(BodyPart.RightArm, 1f, weaponArm: true), Is.True);
            Assert.That(Interrupts(BodyPart.LeftArm, 100f), Is.False);
            Assert.That(Interrupts(BodyPart.Torso, 100f), Is.False);
        }

        [Test]
        public void AboveDamageThreshold_InterruptsOnlyAboveIt()
        {
            _settings.InterruptRule = InterruptRule.AboveDamageThreshold;
            _settings.InterruptDamageThreshold = 10f;

            Assert.That(Interrupts(BodyPart.Torso, 10.5f), Is.True);
            Assert.That(Interrupts(BodyPart.Head, 10f), Is.False);
        }

        [TestCase(InterruptRule.AnyHit)]
        [TestCase(InterruptRule.WeaponArmOrHead)]
        [TestCase(InterruptRule.AboveDamageThreshold)]
        public void SwingArmour_ResistsEveryOption(InterruptRule rule)
        {
            _settings.InterruptRule = rule;
            _mace.HasSwingArmour = true;

            Assert.That(Interrupts(BodyPart.Head, 1000f), Is.False);
        }
    }
}
