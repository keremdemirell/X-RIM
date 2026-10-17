using NUnit.Framework;
using XRim.Core;
using XRim.Rules;
using XRim.Rules.Arena;
using XRim.Rules.Combat;
using XRim.Rules.Match;
using XRim.Rules.Settings;
using XRim.Rules.Signature;

namespace XRim.Tests.EditMode.Rules.Signature
{
    /// <summary>
    /// Where the Kinetic Energy Bar (GDD §8) is filled: the hit rules credit the HP a landed hit removes as damage dealt to the attacker
    /// and damage taken by the victim. Left holds the rapier (torso hit 8), right the mace (torso 20).
    /// </summary>
    public sealed class SignatureChargeAccountingTests
    {
        private const float Tolerance = 1e-5f;
        private const float RapierTorsoDamage = 8f;
        private const float MaceTorsoDamage = 20f;

        private RulesSettings _settings;
        private RulePolicies _policies;
        private MatchState _state;

        [SetUp]
        public void SetUp()
        {
            _settings = GddStartingValues.CreateRulesSettings();
            _policies = new RulePolicies();
            _state = new MatchState(
                new PerSide<FighterState>(
                    new FighterState(Handedness.Right, _settings.Damage.MaxHp, WeaponIds.Rapier),
                    new FighterState(Handedness.Right, _settings.Damage.MaxHp, WeaponIds.Mace)),
                PerSide<ElectricWallState>.Create(_ => new ElectricWallState()));
        }

        private FighterState Left => _state.Fighters[Side.Left];
        private FighterState Right => _state.Fighters[Side.Right];

        private void BarOn() => _settings.Signature.ChargeMode = SignatureChargeMode.KineticEnergyBar;

        private HitResolution Hit(Side attacker, BodyPart part, int milliseconds = 100)
        {
            var resolver = new HitResolver(_state, _settings, _policies,
                new PerSide<TurnAttack>(new TurnAttack(_settings.FindWeapon(WeaponIds.Rapier)),
                    new TurnAttack(_settings.FindWeapon(WeaponIds.Mace))));
            var hit = new HitFacts(attacker, part, attacker == Side.Left ? WeaponIds.Rapier : WeaponIds.Mace,
                SimTime.FromMilliseconds(milliseconds), false);
            return resolver.Resolve(new[] { hit }, new PerSide<bool>(true, true));
        }

        [Test]
        public void ALandedHit_FillsTheAttackersBarByDamageDealt_AndTheVictimsByDamageTaken()
        {
            BarOn();

            Hit(Side.Left, BodyPart.Torso);

            Assert.That(Left.SignatureCharge, Is.EqualTo(RapierTorsoDamage * _settings.Signature.BarChargePerHpDamageDealt).Within(Tolerance));
            Assert.That(Right.SignatureCharge, Is.EqualTo(RapierTorsoDamage * _settings.Signature.BarChargePerHpDamageTaken).Within(Tolerance));
        }

        [Test]
        public void ABiggerHit_FillsMore()
        {
            BarOn();

            Hit(Side.Right, BodyPart.Torso);

            Assert.That(Right.SignatureCharge, Is.EqualTo(MaceTorsoDamage * _settings.Signature.BarChargePerHpDamageDealt).Within(Tolerance));
            Assert.That(Left.SignatureCharge, Is.EqualTo(MaceTorsoDamage * _settings.Signature.BarChargePerHpDamageTaken).Within(Tolerance));
        }

        [Test]
        public void TheHpActuallyRemoved_Counts_NotTheHitsRawDamage()
        {
            // D27: a hit cannot take a dummy from full HP to 0, so it removes less than its damage.
            BarOn();
            _settings.Damage.MaxHp = 10f;
            Left.Hp = 10f;

            HitResolution resolution = Hit(Side.Right, BodyPart.Torso);

            float removed = resolution.Landed[0].Damage.HpDamage;
            Assert.That(removed, Is.LessThan(MaceTorsoDamage));
            Assert.That(Right.SignatureCharge, Is.EqualTo(removed * _settings.Signature.BarChargePerHpDamageDealt).Within(Tolerance));
            Assert.That(Left.SignatureCharge, Is.EqualTo(removed * _settings.Signature.BarChargePerHpDamageTaken).Within(Tolerance));
        }

        [Test]
        public void WithTheOncePerMatchDefault_AHitChargesNothing_AndSpendsNothing()
        {
            Hit(Side.Left, BodyPart.Torso);

            Assert.That(Left.SignatureCharge, Is.EqualTo(0f));
            Assert.That(Right.SignatureCharge, Is.EqualTo(0f));
            Assert.That(Left.SignatureUses, Is.EqualTo(0));
        }

        [Test]
        public void SeveralHits_AddUp_AndTheBarCaps()
        {
            BarOn();
            _settings.Signature.BarChargePerHpDamageTaken = 0.06f; // an 8 HP hit fills 48% of the bar

            // A fresh resolver for every hit (one turn each), so D26's one-hit-per-part rule does not stop the later hits.
            Hit(Side.Left, BodyPart.Torso);
            Hit(Side.Left, BodyPart.Torso);
            Assert.That(Right.SignatureCharge, Is.EqualTo(2f * RapierTorsoDamage * 0.06f).Within(Tolerance));

            Hit(Side.Left, BodyPart.Torso);
            Assert.That(Right.SignatureCharge, Is.EqualTo(KineticEnergyBarChargeModel.FullBar), "1.44 of the bar is a full bar, no more");
        }

        [Test]
        public void ACustomModel_IsToldOfEveryHit()
        {
            var recorder = new RecordingModel();
            _policies.SignatureCharge = recorder;

            Hit(Side.Left, BodyPart.Torso);

            Assert.That(recorder.Dealt, Is.EqualTo(RapierTorsoDamage).Within(Tolerance));
            Assert.That(recorder.Taken, Is.EqualTo(RapierTorsoDamage).Within(Tolerance));
        }

        [Test]
        public void WithoutAModel_HitsStillResolve()
        {
            _policies.SignatureCharge = null;

            Hit(Side.Left, BodyPart.Torso);

            Assert.That(Right.Hp, Is.EqualTo(_settings.Damage.MaxHp - RapierTorsoDamage));
        }

        private sealed class RecordingModel : ISignatureChargeModel
        {
            public float Dealt;
            public float Taken;

            public bool IsAvailable(FighterState fighter, SignatureSettings settings) => false;

            public float ChargeFraction(FighterState fighter, SignatureSettings settings) => 0f;

            public void OnDamageTaken(FighterState fighter, float hpDamage, SignatureSettings settings) => Taken += hpDamage;

            public void OnDamageDealt(FighterState fighter, float hpDamage, SignatureSettings settings) => Dealt += hpDamage;

            public void OnUsed(FighterState fighter, SignatureSettings settings)
            {
            }
        }
    }
}
