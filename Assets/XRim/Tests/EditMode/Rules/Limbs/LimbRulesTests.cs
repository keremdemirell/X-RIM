using System;
using NUnit.Framework;
using XRim.Rules;
using XRim.Rules.Limbs;
using XRim.Rules.Settings;

namespace XRim.Tests.EditMode.Rules.Limbs
{
    /// <summary>Limb durability and the per-hit cap (GDD §11).</summary>
    public sealed class LimbRulesTests
    {
        private const float Tolerance = 1e-4f;

        private readonly LimbRules _limbs = new LimbRules();
        private DamageSettings _settings;

        [SetUp]
        public void SetUp() => _settings = new DamageSettings();

        [TestCase(BodyPart.LeftArm)]
        [TestCase(BodyPart.RightArm)]
        public void Arms_UseTheArmDurability(BodyPart arm) =>
            Assert.That(_limbs.DurabilityOf(arm, _settings), Is.EqualTo(_settings.ArmDurability));

        [TestCase(BodyPart.LeftLeg)]
        [TestCase(BodyPart.RightLeg)]
        public void Legs_UseTheLegDurability(BodyPart leg) =>
            Assert.That(_limbs.DurabilityOf(leg, _settings), Is.EqualTo(_settings.LegDurability));

        [TestCase(BodyPart.Head)]
        [TestCase(BodyPart.Torso)]
        public void HeadAndTorso_HaveNoDurability(BodyPart part) =>
            Assert.Throws<ArgumentException>(() => _limbs.DurabilityOf(part, _settings));

        [Test]
        public void OneHit_AddsAtMostThirtyFivePercentOfDurability()
        {
            Assert.That(_limbs.CapLimbDamage(1000f, 40f, _settings), Is.EqualTo(14f).Within(Tolerance));
            Assert.That(_limbs.CapLimbDamage(10f, 40f, _settings), Is.EqualTo(10f).Within(Tolerance), "below the cap: the whole hit");
            Assert.That(_limbs.CapLimbDamage(-5f, 40f, _settings), Is.EqualTo(0f), "a hit never heals a limb");
        }

        [Test]
        public void TheCap_IsTunable()
        {
            _settings.PerHitLimbCapFraction = 0.25f;

            Assert.That(_limbs.CapLimbDamage(1000f, 40f, _settings), Is.EqualTo(10f).Within(Tolerance));
        }
    }
}
