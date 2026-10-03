using System;
using NUnit.Framework;
using XRim.Core;
using XRim.Rules;
using XRim.Rules.Combat;
using XRim.Rules.Settings;

namespace XRim.Tests.EditMode.Rules.Combat
{
    /// <summary>
    /// The two-stage clash model (GDD §10, Decided): every branch of the flowchart and the exact threshold edges. With the
    /// starting values (W_m 1, W_v 0.005) the mace's power is 10 + 1.25 = 11.25 and the rapier's 2 + 4.5 = 6.5, a ratio of 1.73.
    /// </summary>
    public sealed class ClashResolverTests
    {
        private const float Tolerance = 1e-4f;
        private const float Square = 90f;
        private const float Glancing = 10f;

        private readonly ClashResolver _resolver = new ClashResolver();
        private ClashSettings _settings;
        private WeaponStats _rapier;
        private WeaponStats _mace;

        [SetUp]
        public void SetUp()
        {
            _settings = new ClashSettings();
            _rapier = GddStartingValues.Rapier();
            _mace = GddStartingValues.Mace();
        }

        private ClashResult Resolve(float angle, float leftMass, float leftSpeed, float rightMass, float rightSpeed) =>
            _resolver.Resolve(new ClashFacts(angle, new ClashParticipant(Side.Left, leftMass, leftSpeed),
                new ClashParticipant(Side.Right, rightMass, rightSpeed)), _settings);

        private ClashResult Resolve(float angle, WeaponStats left, WeaponStats right) =>
            Resolve(angle, left.Mass, left.SpeedUnitsPerSecond, right.Mass, right.SpeedUnitsPerSecond);

        /// <summary>Exact binary weights, so a power ratio of exactly 1.5 is exactly 1.5.</summary>
        private void UseExactWeights()
        {
            _settings.MassWeight = 1f;
            _settings.SpeedWeight = 0.5f;
        }

        // --- The four outcomes with the GDD weapons ----------------------------------------------

        [Test]
        public void MaceAgainstRapier_SquareOn_TheMaceCrushesThrough_AndStaggersTheRapier()
        {
            ClashResult result = Resolve(Square, _mace, _rapier);

            Assert.That(result.Kind, Is.EqualTo(ClashKind.CrushThrough));
            Assert.That(result.Winner, Is.EqualTo(Side.Left));
            Assert.That(result.IsHardClash, Is.True);
            Assert.That(result.LeftPower, Is.EqualTo(11.25f).Within(Tolerance));
            Assert.That(result.RightPower, Is.EqualTo(6.5f).Within(Tolerance));
            Assert.That(result.Continues(Side.Left), Is.True);
            Assert.That(result.Continues(Side.Right), Is.False);
            Assert.That(result.IsKnockedOff(Side.Right), Is.True);
            Assert.That(result.Staggers(Side.Right), Is.True);
            Assert.That(result.Staggers(Side.Left), Is.False);
        }

        [Test]
        public void TwoRapiers_SquareOn_BothRebound_AndNeitherContinues()
        {
            ClashResult result = Resolve(Square, _rapier, _rapier);

            Assert.That(result.Kind, Is.EqualTo(ClashKind.BothRebound));
            Assert.That(result.Winner, Is.Null);
            Assert.That(result.Rebounds, Is.True);
            foreach (Side side in new[] { Side.Left, Side.Right })
            {
                Assert.That(result.Continues(side), Is.False);
                Assert.That(result.IsKnockedOff(side), Is.False, "a rebound is not a knock-off");
                Assert.That(result.Staggers(side), Is.False);
            }
        }

        [Test]
        public void RapierAgainstMace_Glancing_TheLighterRapierDeflectsTheMace_WithoutAStagger()
        {
            ClashResult result = Resolve(Glancing, _rapier, _mace);

            Assert.That(result.Kind, Is.EqualTo(ClashKind.LighterDeflectsHeavier));
            Assert.That(result.Winner, Is.EqualTo(Side.Left));
            Assert.That(result.IsHardClash, Is.False);
            Assert.That(result.Continues(Side.Left), Is.True);
            Assert.That(result.IsKnockedOff(Side.Right), Is.True, "the deflected heavy weapon misses");
            Assert.That(result.Staggers(Side.Right), Is.False, "only a crush-through staggers");
        }

        [Test]
        public void TwoRapiers_Glancing_BothSlidePast_AndContinue()
        {
            ClashResult result = Resolve(Glancing, _rapier, _rapier);

            Assert.That(result.Kind, Is.EqualTo(ClashKind.BothSlidePast));
            Assert.That(result.Winner, Is.Null);
            Assert.That(result.Continues(Side.Left), Is.True);
            Assert.That(result.Continues(Side.Right), Is.True);
            Assert.That(result.IsKnockedOff(Side.Left) || result.IsKnockedOff(Side.Right), Is.False);
        }

        // --- Stage 1: the angle -----------------------------------------------------------------

        [TestCase(90f, ClashKind.CrushThrough)]
        [TestCase(30f, ClashKind.CrushThrough)]
        [TestCase(29.99f, ClashKind.LighterDeflectsHeavier)]
        [TestCase(0f, ClashKind.LighterDeflectsHeavier)]
        public void Stage1_AnAngleAtOrAboveThirtyDegrees_IsAHardClash(float angle, ClashKind expected)
        {
            Assert.That(_settings.HardClashAngleDegrees, Is.EqualTo(30f));

            Assert.That(Resolve(angle, _mace, _rapier).Kind, Is.EqualTo(expected));
        }

        // --- Stage 2, hard clash: the power ratio ---------------------------------------------------

        [TestCase(4f, ClashKind.CrushThrough)]
        [TestCase(3.998f, ClashKind.BothRebound)]
        public void Stage2_TheCrushRatio_IsReachedAtExactlyOnePointFive(float leftSpeed, ClashKind expected)
        {
            UseExactWeights();

            // Left: 1 + 0.5 × 4 = 3 (or 2.999); right: 2 + 0 = 2.
            ClashResult result = Resolve(Square, 1f, leftSpeed, 2f, 0f);

            Assert.That(result.Kind, Is.EqualTo(expected));
            Assert.That(result.Winner, Is.EqualTo(expected == ClashKind.CrushThrough ? Side.Left : (Side?)null));
        }

        [Test]
        public void Stage2_EqualPowers_Rebound()
        {
            Assert.That(Resolve(Square, 5f, 300f, 5f, 300f).Kind, Is.EqualTo(ClashKind.BothRebound));
        }

        [Test]
        public void Stage2_TheStrongerWeapon_WinsOnEitherSide()
        {
            ClashResult result = Resolve(Square, _rapier, _mace);

            Assert.That(result.Kind, Is.EqualTo(ClashKind.CrushThrough));
            Assert.That(result.Winner, Is.EqualTo(Side.Right));
            Assert.That(result.Staggers(Side.Left), Is.True);
        }

        [Test]
        public void Stage2_Power_IsMassWeightTimesMass_PlusSpeedWeightTimesSpeed()
        {
            UseExactWeights();

            ClashResult result = Resolve(Square, 3f, 10f, 1f, 2f);

            Assert.That(result.LeftPower, Is.EqualTo(3f + 0.5f * 10f).Within(Tolerance));
            Assert.That(result.RightPower, Is.EqualTo(1f + 0.5f * 2f).Within(Tolerance));
            Assert.That(ClashResolver.Power(new ClashParticipant(Side.Left, 3f, 10f), _settings), Is.EqualTo(8f).Within(Tolerance));
        }

        [Test]
        public void Stage2_AHardClash_IsDecidedByPower_SoSpeedCanBeatEqualMass()
        {
            UseExactWeights();

            ClashResult result = Resolve(Square, 2f, 10f, 2f, 0f);

            Assert.That(result.Kind, Is.EqualTo(ClashKind.CrushThrough));
            Assert.That(result.Winner, Is.EqualTo(Side.Left), "7 against 2: the faster weapon crushes");
        }

        [Test]
        public void Stage2_AWeaponWithNoPower_IsCrushedByAnyPower_AndTwoWithoutPowerRebound()
        {
            Assert.That(Resolve(Square, 0.1f, 0f, 0f, 0f).Winner, Is.EqualTo(Side.Left));
            Assert.That(Resolve(Square, 0f, 0f, 0f, 0f).Kind, Is.EqualTo(ClashKind.BothRebound));
        }

        // --- Stage 2, glancing: the mass band -------------------------------------------------------

        [TestCase(5f, ClashKind.BothSlidePast)]
        [TestCase(5.001f, ClashKind.LighterDeflectsHeavier)]
        public void Glancing_TheSimilarMassBand_IncludesItsEdge(float heavierMass, ClashKind expected)
        {
            _settings.GlancingSimilarMassBandFraction = 0.25f;

            // 4 × 1.25 = 5: exactly at the edge of the band.
            ClashResult result = Resolve(Glancing, 4f, 100f, heavierMass, 100f);

            Assert.That(result.Kind, Is.EqualTo(expected));
            if (expected == ClashKind.LighterDeflectsHeavier) Assert.That(result.Winner, Is.EqualTo(Side.Left));
        }

        [Test]
        public void Glancing_IsDecidedByMass_NotPower_SoTheLighterWinsOnEitherSide()
        {
            // The heavy left weapon is also far faster, so it has far more power; glancing, agility still wins.
            ClashResult result = Resolve(Glancing, 10f, 2000f, 2f, 0f);

            Assert.That(result.Kind, Is.EqualTo(ClashKind.LighterDeflectsHeavier));
            Assert.That(result.Winner, Is.EqualTo(Side.Right));
            Assert.That(result.IsKnockedOff(Side.Left), Is.True);
        }

        // --- Guards ------------------------------------------------------------------------

        [Test]
        public void TheResult_CarriesTheAngle()
        {
            Assert.That(Resolve(42f, _mace, _rapier).ContactAngleDegrees, Is.EqualTo(42f));
        }

        [Test]
        public void ParticipantsOnTheWrongSides_AreRefused()
        {
            var swapped = new ClashFacts(Square, new ClashParticipant(Side.Right, 2f, 900f), new ClashParticipant(Side.Left, 10f, 250f));

            Assert.Throws<ArgumentException>(() => _resolver.Resolve(swapped, _settings));
        }

        [Test]
        public void AResult_NeedsAWinnerExactlyWhenOneWeaponComesOutAhead()
        {
            Assert.Throws<ArgumentException>(() => new ClashResult(ClashKind.BothRebound, Side.Left, Square, 1f, 1f));
            Assert.Throws<ArgumentException>(() => new ClashResult(ClashKind.CrushThrough, null, Square, 1f, 1f));
        }
    }
}
