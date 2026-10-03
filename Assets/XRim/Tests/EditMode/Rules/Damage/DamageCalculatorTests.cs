using System.Collections.Generic;
using NUnit.Framework;
using XRim.Core;
using XRim.Rules;
using XRim.Rules.Combat;
using XRim.Rules.Damage;
using XRim.Rules.Match;
using XRim.Rules.Settings;

namespace XRim.Tests.EditMode.Rules.Damage
{
    /// <summary>
    /// Damage = BaseDamage × ZoneMultiplier × Modifiers (GDD §11), the per-hit limb cap, the head stun and the no-instant-KO
    /// rule (D27). The victim starts below full HP, so D27 only clamps where a test sets it to full HP.
    /// </summary>
    public sealed class DamageCalculatorTests
    {
        private const float Tolerance = 1e-4f;
        private const float DamagedHp = 60f;

        /// <summary>Far more damage than any HP or durability, to show what the caps do.</summary>
        private const float HugeBaseDamage = 1000f;

        private RulesSettings _settings;
        private RulePolicies _policies;
        private DamageCalculator _calculator;
        private FighterState _victim;
        private WeaponStats _rapier;
        private WeaponStats _mace;

        [SetUp]
        public void SetUp()
        {
            _settings = GddStartingValues.CreateRulesSettings();
            _policies = new RulePolicies();
            _calculator = new DamageCalculator(_policies.InstantKo);
            _victim = new FighterState(Handedness.Right, DamagedHp, WeaponIds.Rapier);
            _rapier = _settings.FindWeapon(WeaponIds.Rapier);
            _mace = _settings.FindWeapon(WeaponIds.Mace);
        }

        private static HitFacts Hit(BodyPart part, WeaponStats weapon, bool offHand = false) =>
            new HitFacts(Side.Left, part, weapon.WeaponId, SimTime.FromSeconds(0.2), offHand);

        private DamageResult Calculate(BodyPart part, WeaponStats weapon = null, bool offHand = false, BodyMoveStats move = null,
            bool crushedThrough = false, IReadOnlyList<IDamageModifier> modifiers = null)
        {
            weapon = weapon ?? _mace;
            var context = new DamageContext(Hit(part, weapon, offHand), weapon, _settings, move, crushedThrough);
            return _calculator.Calculate(context, modifiers ?? _policies.DamageModifiers, _victim);
        }

        private static WeaponStats Huge() => new WeaponStats { Id = "huge", BaseDamage = HugeBaseDamage, SpeedUnitsPerSecond = 1f };

        private void AtFullHp() => _victim.Hp = _settings.Damage.MaxHp;

        // --- The formula (§11) -----------------------------------------------------------------

        [TestCase(BodyPart.Head, 2.5f)]
        [TestCase(BodyPart.Torso, 1.0f)]
        [TestCase(BodyPart.LeftArm, 0.8f)]
        [TestCase(BodyPart.RightArm, 0.8f)]
        [TestCase(BodyPart.LeftLeg, 0.7f)]
        [TestCase(BodyPart.RightLeg, 0.7f)]
        public void EveryZone_IsBaseDamageTimesItsMultiplier(BodyPart part, float appendixAMultiplier)
        {
            DamageResult result = Calculate(part);

            float expected = _mace.BaseDamage * appendixAMultiplier;
            Assert.That(result.Damage, Is.EqualTo(expected).Within(Tolerance));
            Assert.That(result.HpDamage, Is.EqualTo(expected).Within(Tolerance), "one global HP pool takes the whole hit");
        }

        [Test]
        public void ZoneMultipliers_AreTunable()
        {
            _settings.HitZones.HeadMultiplier = 3f;

            Assert.That(Calculate(BodyPart.Head).Damage, Is.EqualTo(_mace.BaseDamage * 3f).Within(Tolerance));
        }

        [Test]
        public void Damage_ComesFromDesignerBaseDamage_NeverFromPhysicsEnergy()
        {
            float rapierEnergy = 0.5f * _rapier.Mass * _rapier.SpeedUnitsPerSecond * _rapier.SpeedUnitsPerSecond;
            float maceEnergy = 0.5f * _mace.Mass * _mace.SpeedUnitsPerSecond * _mace.SpeedUnitsPerSecond;
            Assert.That(rapierEnergy, Is.GreaterThan(maceEnergy), "precondition: ½mv² would invert the balance (§11, Rejected)");

            Assert.That(Calculate(BodyPart.Torso, _rapier).Damage, Is.EqualTo(_rapier.BaseDamage).Within(Tolerance));
            Assert.That(Calculate(BodyPart.Torso, _mace).Damage, Is.EqualTo(_mace.BaseDamage).Within(Tolerance));
            Assert.That(Calculate(BodyPart.Torso, _rapier.WithSpeedMultiplier(2f)).Damage, Is.EqualTo(_rapier.BaseDamage).Within(Tolerance),
                "a faster weapon arrives sooner, it does not hit harder");
        }

        // --- Modifiers -------------------------------------------------------------------------

        [Test]
        public void OffHand_DealsTheOffHandShare()
        {
            Assert.That(Calculate(BodyPart.Torso, offHand: true).Damage,
                Is.EqualTo(_mace.BaseDamage * _settings.Damage.OffHandDamageMultiplier).Within(Tolerance));
            Assert.That(_settings.Damage.OffHandDamageMultiplier, Is.EqualTo(0.8f).Within(Tolerance), "§12: 80%");
        }

        [Test]
        public void CrushThrough_DealsThirtyPercentLess()
        {
            Assert.That(Calculate(BodyPart.Torso, crushedThrough: true).Damage, Is.EqualTo(_mace.BaseDamage * 0.7f).Within(Tolerance));
        }

        [Test]
        public void Modifiers_Multiply()
        {
            Assert.That(Calculate(BodyPart.Torso, offHand: true, crushedThrough: true).Damage,
                Is.EqualTo(_mace.BaseDamage * 0.8f * 0.7f).Within(Tolerance));
        }

        [Test]
        public void ShieldReduction_ScalesAHitAfterAPartialBlock_AndStacksWithCrushThroughAndFollowThrough()
        {
            var context = new DamageContext(Hit(BodyPart.Torso, _mace), _mace, _settings, null, true, 0.5f, 0.5f);

            Assert.That(_calculator.Calculate(context, _policies.DamageModifiers, _victim).Damage,
                Is.EqualTo(_mace.BaseDamage * 0.7f * 0.5f * 0.5f).Within(Tolerance), "20 × 0.7 crush × 0.5 shield × 0.5 speed left");
            Assert.That(Calculate(BodyPart.Torso).Damage, Is.EqualTo(_mace.BaseDamage).Within(Tolerance), "no block: untouched");
        }

        [Test]
        public void TheModifiers_RunInTheDocumentedOrder()
        {
            var order = new System.Type[]
            {
                typeof(OffHandDamageModifier), typeof(CrushThroughDamageModifier), typeof(ShieldBlockDamageModifier),
                typeof(BodyMoveDamageBonusModifier), typeof(FollowThroughDamageModifier),
            };

            Assert.That(System.Linq.Enumerable.Select(_policies.DamageModifiers, modifier => modifier.GetType()), Is.EqualTo(order));
        }

        [Test]
        public void BodyMoveDamageBonus_D13_AddsNothingByDefault_AndScalesTheHitWhenSet()
        {
            BodyMoveStats lunge = _settings.FindBodyMove(BodyMove.Lunge);
            Assert.That(Calculate(BodyPart.Torso, move: lunge).Damage, Is.EqualTo(_mace.BaseDamage).Within(Tolerance), "D13 default: reach only");

            lunge.DamageBonusFraction = 0.25f;
            Assert.That(Calculate(BodyPart.Torso, move: lunge).Damage, Is.EqualTo(_mace.BaseDamage * 1.25f).Within(Tolerance));
        }

        [Test]
        public void FollowThrough_D26_ScalesALaterHitByTheSpeedLeft()
        {
            var context = new DamageContext(Hit(BodyPart.Torso, _mace), _mace, _settings, followThroughSpeedFraction: 0.49f);

            Assert.That(_calculator.Calculate(context, _policies.DamageModifiers, _victim).Damage,
                Is.EqualTo(_mace.BaseDamage * 0.49f).Within(Tolerance));
        }

        [Test]
        public void TheModifierList_IsTheSeam()
        {
            Assert.That(Calculate(BodyPart.Torso, offHand: true, modifiers: new IDamageModifier[0]).Damage,
                Is.EqualTo(_mace.BaseDamage).Within(Tolerance), "no modifiers: the plain formula");
            Assert.That(Calculate(BodyPart.Torso, modifiers: new IDamageModifier[] { new Doubling() }).Damage,
                Is.EqualTo(_mace.BaseDamage * 2f).Within(Tolerance));
        }

        // --- Limb damage (§11) -----------------------------------------------------------------

        [Test]
        public void ALimbHit_AddsAtMostThePerHitCapOfItsDurability_ButHpTakesTheWholeHit()
        {
            DamageResult result = Calculate(BodyPart.RightArm);

            float cap = _settings.Damage.ArmDurability * _settings.Damage.PerHitLimbCapFraction;
            Assert.That(_mace.BaseDamage * 0.8f, Is.GreaterThan(cap), "precondition: the mace's arm hit is above the cap");
            Assert.That(result.LimbDamage, Is.EqualTo(cap).Within(Tolerance));
            Assert.That(result.HpDamage, Is.EqualTo(_mace.BaseDamage * 0.8f).Within(Tolerance));
        }

        [Test]
        public void ASmallLimbHit_AddsItsWholeDamage_AndLegsUseTheirOwnDurability()
        {
            DamageResult result = Calculate(BodyPart.LeftLeg, _rapier);

            Assert.That(result.LimbDamage, Is.EqualTo(_rapier.BaseDamage * 0.7f).Within(Tolerance));
            Assert.That(Calculate(BodyPart.LeftLeg, Huge()).LimbDamage,
                Is.EqualTo(_settings.Damage.LegDurability * _settings.Damage.PerHitLimbCapFraction).Within(Tolerance));
        }

        [TestCase(BodyPart.Head)]
        [TestCase(BodyPart.Torso)]
        public void HeadAndTorso_TrackNoLimbDamage(BodyPart part)
        {
            DamageResult result = Calculate(part, Huge());

            Assert.That(result.LimbDamage, Is.EqualTo(0f));
            Assert.That(result.SeversLimb, Is.False, "§11: head and torso cannot be severed");
        }

        [TestCase(BodyPart.LeftArm)]
        [TestCase(BodyPart.RightArm)]
        [TestCase(BodyPart.LeftLeg)]
        [TestCase(BodyPart.RightLeg)]
        public void Severing_NeedsAtLeastThreeHits_HoweverHardEachOneIs(BodyPart limb)
        {
            for (int hit = 1; hit <= 3; hit++)
            {
                DamageResult result = Calculate(limb, Huge());
                Assert.That(result.SeversLimb, Is.EqualTo(hit == 3), $"hit {hit}");
                DamageCalculator.ApplyTo(_victim, limb, result);
            }
        }

        [Test]
        public void ReachingDurability_IsReportedOnce_AndLimbDamageNeverHeals()
        {
            for (int hit = 0; hit < 3; hit++)
            {
                DamageCalculator.ApplyTo(_victim, BodyPart.LeftArm, Calculate(BodyPart.LeftArm, Huge()));
            }

            float total = _victim.GetLimbDamage(BodyPart.LeftArm);
            DamageResult fourth = Calculate(BodyPart.LeftArm, Huge());

            Assert.That(total, Is.GreaterThanOrEqualTo(_settings.Damage.ArmDurability));
            Assert.That(fourth.SeversLimb, Is.False, "the fact is reported by the hit that reaches durability");
            Assert.That(_victim.GetLimbDamage(BodyPart.RightArm), Is.EqualTo(0f), "each limb keeps its own total");
        }

        [Test]
        public void Calculate_LeavesTheVictimUntouched_ApplyChangesIt()
        {
            DamageResult result = Calculate(BodyPart.LeftArm);
            Assert.That(_victim.Hp, Is.EqualTo(DamagedHp));
            Assert.That(_victim.GetLimbDamage(BodyPart.LeftArm), Is.EqualTo(0f));

            DamageCalculator.ApplyTo(_victim, BodyPart.LeftArm, result);
            Assert.That(_victim.Hp, Is.EqualTo(DamagedHp - result.HpDamage).Within(Tolerance));
            Assert.That(_victim.GetLimbDamage(BodyPart.LeftArm), Is.EqualTo(result.LimbDamage).Within(Tolerance));
        }

        // --- No instant KO (§11, D27) ----------------------------------------------------------

        [TestCase(BodyPart.Head)]
        [TestCase(BodyPart.Torso)]
        [TestCase(BodyPart.LeftLeg)]
        public void D27_NoSingleHit_TakesADummyFromFullHpToZero(BodyPart part)
        {
            AtFullHp();

            DamageResult result = Calculate(part, Huge());
            DamageCalculator.ApplyTo(_victim, part, result);

            Assert.That(result.WasClampedByNoInstantKo, Is.True);
            Assert.That(_victim.Hp, Is.EqualTo(1f).Within(Tolerance), "clamped to leave 1 HP");
            Assert.That(_victim.IsDead, Is.False);
        }

        [Test]
        public void D27_ALaterHit_CanFinishADamagedDummy()
        {
            AtFullHp();
            DamageCalculator.ApplyTo(_victim, BodyPart.Head, Calculate(BodyPart.Head, Huge()));

            DamageResult second = Calculate(BodyPart.Head, Huge());
            DamageCalculator.ApplyTo(_victim, BodyPart.Head, second);

            Assert.That(second.WasClampedByNoInstantKo, Is.False);
            Assert.That(_victim.IsDead, Is.True);
        }

        [Test]
        public void D27_AnOrdinaryHitFromFullHp_IsUntouched()
        {
            AtFullHp();

            DamageResult result = Calculate(BodyPart.Head);

            Assert.That(result.HpDamage, Is.EqualTo(result.Damage), "the mace's head hit is far below 100 HP");
        }

        [Test]
        public void D27_InSuddenDeath_TheFirstHitStillKills()
        {
            _victim.Hp = RuleConstants.SuddenDeathHp;

            DamageCalculator.ApplyTo(_victim, BodyPart.LeftLeg, Calculate(BodyPart.LeftLeg, _rapier));

            Assert.That(_victim.IsDead, Is.True, "§14: with 1 HP the first valid hit wins");
        }

        [Test]
        public void D27_IsAPolicySeam()
        {
            AtFullHp();
            var calculator = new DamageCalculator(new NoGuard());
            var context = new DamageContext(Hit(BodyPart.Head, Huge()), Huge(), _settings);

            Assert.That(_policies.InstantKo, Is.InstanceOf<NoKoFromFullHpPolicy>(), "the D27 default");
            Assert.That(calculator.Calculate(context, _policies.DamageModifiers, _victim).HpDamage, Is.EqualTo(HugeBaseDamage * 2.5f).Within(Tolerance));
        }

        // --- Head stun (§11) -------------------------------------------------------------------

        [Test]
        public void AHeadHitAboveTheThreshold_Stuns_AWeakerOneDoesNot()
        {
            Assert.That(_mace.BaseDamage * 2.5f, Is.GreaterThan(_settings.Damage.HeadStunThreshold), "precondition");
            Assert.That(_rapier.BaseDamage * 2.5f, Is.LessThan(_settings.Damage.HeadStunThreshold), "precondition");

            Assert.That(Calculate(BodyPart.Head, _mace).Stuns, Is.True);
            Assert.That(Calculate(BodyPart.Head, _rapier).Stuns, Is.False);
        }

        [Test]
        public void TheStun_NeedsDamageStrictlyAboveTheThreshold()
        {
            _settings.Damage.HeadStunThreshold = _mace.BaseDamage * 2.5f;
            Assert.That(Calculate(BodyPart.Head).Stuns, Is.False, "exactly at the threshold: not above it");

            _settings.Damage.HeadStunThreshold -= 0.01f;
            Assert.That(Calculate(BodyPart.Head).Stuns, Is.True);
        }

        [TestCase(BodyPart.Torso)]
        [TestCase(BodyPart.RightArm)]
        [TestCase(BodyPart.LeftLeg)]
        public void OnlyHeadHits_Stun(BodyPart part)
        {
            Assert.That(Calculate(part, Huge()).Stuns, Is.False);
        }

        [Test]
        public void AHeadHitClampedByD27_StillStuns()
        {
            AtFullHp();

            DamageResult result = Calculate(BodyPart.Head, Huge());

            Assert.That(result.WasClampedByNoInstantKo, Is.True);
            Assert.That(result.Stuns, Is.True, "§11: no instant KO, but a strong head hit stuns");
        }

        // --- Test doubles ----------------------------------------------------------------------

        private sealed class Doubling : IDamageModifier
        {
            public float Modify(float damage, DamageContext context) => damage * 2f;
        }

        private sealed class NoGuard : IInstantKoPolicy
        {
            public float LimitHpDamage(float damage, HitFacts hit, FighterState victim, DamageSettings settings) => damage;
        }
    }
}
