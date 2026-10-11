using NUnit.Framework;
using XRim.Core;
using XRim.Rules;
using XRim.Rules.Limbs;
using XRim.Rules.Match;
using XRim.Rules.Paths;
using XRim.Rules.Settings;

namespace XRim.Tests.EditMode.Rules.Limbs
{
    /// <summary>
    /// The §12 seams and their defaults (designer, 2026-10-10): the drawn path steers the head without arms; picking the club retrieves
    /// a limb automatically, even out of reach; the body blow reaches only its own length.
    /// </summary>
    public sealed class LimbPolicyDefaultsTests
    {
        private RulesSettings _settings;
        private FighterState _fighter;

        [SetUp]
        public void SetUp()
        {
            _settings = GddStartingValues.CreateRulesSettings();
            _fighter = new FighterState(Handedness.Right, _settings.Damage.MaxHp, WeaponIds.SeveredLimb);
        }

        private float ClubReach => ReachLimit.For(_settings.Paths, _settings.FindWeapon(WeaponIds.SeveredLimb)).RadiusUnits;

        [Test]
        public void RulePolicies_UseTheDefaults()
        {
            var policies = new RulePolicies();

            Assert.That(policies.ArmlessAttack, Is.InstanceOf<HeadbuttArmlessAttackMode>());
            Assert.That(policies.LimbRetrieval, Is.InstanceOf<SettingsLimbRetrievalPolicy>());
            Assert.That(new LimbLossSettings().Retrieval, Is.EqualTo(LimbRetrievalRule.Automatic));
        }

        [Test]
        public void WithoutArms_TheHeadStrikes_WhateverIsDrawn()
        {
            var headbutt = new HeadbuttArmlessAttackMode();
            var low = new WeaponPath(new[] { new Vec2(50f, 100f), new Vec2(150f, 10f) });

            Assert.That(headbutt.ChooseStrikingPart(_fighter, WeaponPath.Empty), Is.EqualTo(BodyPart.Head));
            Assert.That(headbutt.ChooseStrikingPart(_fighter, low), Is.EqualTo(BodyPart.Head), "even a path aimed low");
        }

        [TestCase(0f)]
        [TestCase(1000f)]
        [TestCase(float.MaxValue)]
        public void Retrieval_ByDefault_IsAutomatic_EvenOutOfReach(float distance) =>
            Assert.That(new SettingsLimbRetrievalPolicy().CanRetrieve(_fighter, distance, _settings), Is.True);

        [Test]
        public void Retrieval_WithinReach_KeepsToTheClubsReach()
        {
            _settings.Damage.LimbLoss.Retrieval = LimbRetrievalRule.WithinReach;
            var policy = new SettingsLimbRetrievalPolicy();

            Assert.That(ClubReach, Is.EqualTo(_settings.Paths.ArmLengthUnits + _settings.FindWeapon(WeaponIds.SeveredLimb).LengthUnits));
            Assert.That(policy.CanRetrieve(_fighter, ClubReach, _settings), Is.True);
            Assert.That(policy.CanRetrieve(_fighter, ClubReach + 1f, _settings), Is.False);
        }

        [Test]
        public void TheBodyBlow_ReachesItsOwnLength_FromTheShoulder()
        {
            WeaponStats bodyBlow = _settings.FindWeapon(WeaponIds.BodyBlow);

            ReachLimit reach = ReachLimit.For(_settings.Paths, bodyBlow);

            Assert.That(bodyBlow.Kind, Is.EqualTo(WeaponKind.BodyBlow));
            Assert.That(bodyBlow.DesignIsTbd, Is.True, "§12: its stats are TBD");
            Assert.That(reach.OriginUnits, Is.EqualTo(_settings.Paths.ShoulderOffsetUnits));
            Assert.That(reach.RadiusUnits, Is.EqualTo(bodyBlow.LengthUnits), "no arm adds to it");
        }
    }
}
