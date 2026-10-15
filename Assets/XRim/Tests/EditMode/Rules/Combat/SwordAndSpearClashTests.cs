using System.Collections.Generic;
using NUnit.Framework;
using XRim.Core;
using XRim.Rules;
using XRim.Rules.Combat;
using XRim.Rules.Settings;

namespace XRim.Tests.EditMode.Rules.Combat
{
    /// <summary>
    /// The sword and the spear clash and block through their mass and speed alone (Session 14, GDD §7, §10): no rule names a
    /// weapon. With the starting values (W_m 1, W_v 0.005, crush ratio 1.5) the powers are rapier 6.5, spear 7.5, sword 8 and
    /// mace 11.25, so no pair among the three blades reaches the crush ratio, and the mace crushes the rapier (1.73) but only
    /// rebounds off the sword (1.41). The mace against the spear lands exactly on the crush ratio (11.25 / 7.5 = 1.5): an edge
    /// the exact-edge tests of <c>ClashResolverTests</c> cover, left out here on purpose.
    /// </summary>
    public sealed class SwordAndSpearClashTests
    {
        private const float Square = 90f;
        private const float Glancing = 10f;
        private const float MiddleOfTheFace = 0f;
        private const float OnTheRim = 0.95f;

        private readonly ClashResolver _resolver = new ClashResolver();
        private ClashSettings _settings;
        private WeaponStats _rapier;
        private WeaponStats _sword;
        private WeaponStats _spear;
        private WeaponStats _mace;

        [SetUp]
        public void SetUp()
        {
            _settings = new ClashSettings();
            _rapier = GddStartingValues.Rapier();
            _sword = GddStartingValues.Sword();
            _spear = GddStartingValues.Spear();
            _mace = GddStartingValues.Mace();
        }

        private ClashResult Resolve(float angle, WeaponStats left, WeaponStats right, float leftSpeed = -1f, float rightSpeed = -1f) =>
            _resolver.Resolve(new ClashFacts(angle,
                new ClashParticipant(Side.Left, left.Mass, leftSpeed >= 0f ? leftSpeed : left.SpeedUnitsPerSecond),
                new ClashParticipant(Side.Right, right.Mass, rightSpeed >= 0f ? rightSpeed : right.SpeedUnitsPerSecond)), _settings);

        // --- Hard clashes: power from mass and speed --------------------------------------------------

        [Test]
        public void TheStartingPowers_AreMassTimesOneAndSpeedTimesHalfAPercent()
        {
            Assert.That(ClashResolver.Power(Participant(_rapier), _settings), Is.EqualTo(6.5f).Within(1e-4f));
            Assert.That(ClashResolver.Power(Participant(_spear), _settings), Is.EqualTo(7.5f).Within(1e-4f));
            Assert.That(ClashResolver.Power(Participant(_sword), _settings), Is.EqualTo(8f).Within(1e-4f));
            Assert.That(ClashResolver.Power(Participant(_mace), _settings), Is.EqualTo(11.25f).Within(1e-4f));
        }

        [TestCase("sword", "rapier")]
        [TestCase("spear", "rapier")]
        [TestCase("sword", "spear")]
        [TestCase("sword", "sword")]
        [TestCase("spear", "spear")]
        public void TwoBlades_SquareOn_ReboundBecauseNeitherReachesTheCrushRatio(string left, string right)
        {
            ClashResult result = Resolve(Square, Weapon(left), Weapon(right));

            Assert.That(result.Kind, Is.EqualTo(ClashKind.BothRebound));
            Assert.That(result.Winner, Is.Null);
        }

        [Test]
        public void TheMace_SquareOn_ReboundsOffTheSword_ButCrushesTheRapier()
        {
            Assert.That(Resolve(Square, _mace, _sword).Kind, Is.EqualTo(ClashKind.BothRebound), "11.25 against 8 is 1.41");
            Assert.That(Resolve(Square, _mace, _rapier).Kind, Is.EqualTo(ClashKind.CrushThrough), "11.25 against 6.5 is 1.73");
        }

        [Test]
        public void ASwordThrownAtThreeTimesItsSpeed_CrushesTheRapier()
        {
            Assert.That(Resolve(Square, _sword, _rapier).Kind, Is.EqualTo(ClashKind.BothRebound));

            ClashResult fast = Resolve(Square, _sword, _rapier, leftSpeed: _sword.SpeedUnitsPerSecond * 3f);

            Assert.That(fast.Kind, Is.EqualTo(ClashKind.CrushThrough), "speed alone flips it: 5 + 9 = 14 against 6.5");
            Assert.That(fast.Winner, Is.EqualTo(Side.Left));
        }

        [Test]
        public void AStillWeapon_ClashesWithItsMassOnly()
        {
            // A1: a weapon not travelling its path has speed 0, so a held rapier (power 2) is crushed by a thrust.
            ClashResult heldRapier = Resolve(Square, _sword, _rapier, rightSpeed: 0f);
            ClashResult heldSword = Resolve(Square, _spear, _sword, rightSpeed: 0f);

            Assert.That(heldRapier.Kind, Is.EqualTo(ClashKind.CrushThrough));
            Assert.That(heldRapier.Winner, Is.EqualTo(Side.Left));
            Assert.That(heldRapier.RightPower, Is.EqualTo(_rapier.Mass).Within(1e-4f));
            Assert.That(heldSword.RightPower, Is.EqualTo(_sword.Mass).Within(1e-4f));
        }

        // --- Glancing clashes: the lighter weapon wins ----------------------------------------------------

        [TestCase("sword", "rapier", Side.Right)]
        [TestCase("spear", "rapier", Side.Right)]
        [TestCase("sword", "spear", Side.Right)]
        [TestCase("mace", "sword", Side.Right)]
        [TestCase("mace", "spear", Side.Right)]
        [TestCase("rapier", "sword", Side.Left)]
        public void Glancing_TheLighterWeaponDeflectsTheHeavier(string left, string right, Side winner)
        {
            ClashResult result = Resolve(Glancing, Weapon(left), Weapon(right));

            Assert.That(result.Kind, Is.EqualTo(ClashKind.LighterDeflectsHeavier));
            Assert.That(result.Winner, Is.EqualTo(winner));
        }

        [Test]
        public void Glancing_TwoSwords_SlidePast()
        {
            Assert.That(Resolve(Glancing, _sword, _sword).Kind, Is.EqualTo(ClashKind.BothSlidePast));
        }

        // --- The model never looks at which weapon it is -----------------------------------------------

        [Test]
        public void EveryPairOfTheRoster_ResolvesFromMassAndSpeedAlone_AtBothAngles()
        {
            var roster = new List<WeaponStats> { _rapier, _sword, _spear, _mace };
            foreach (WeaponStats left in roster)
            {
                foreach (WeaponStats right in roster)
                {
                    float leftPower = _settings.MassWeight * left.Mass + _settings.SpeedWeight * left.SpeedUnitsPerSecond;
                    float rightPower = _settings.MassWeight * right.Mass + _settings.SpeedWeight * right.SpeedUnitsPerSecond;

                    // A pair at (or one float step from) the crush ratio is left to the exact-edge tests of ClashResolverTests.
                    float ratio = System.Math.Max(leftPower, rightPower) / System.Math.Min(leftPower, rightPower);
                    if (System.Math.Abs(ratio - _settings.CrushRatio) < 1e-3f) continue;

                    ClashKind hard = ratio >= _settings.CrushRatio ? ClashKind.CrushThrough : ClashKind.BothRebound;
                    Assert.That(Resolve(Square, left, right).Kind, Is.EqualTo(hard), $"{left.Id} against {right.Id}, square");

                    float heavier = System.Math.Max(left.Mass, right.Mass);
                    float lighter = System.Math.Min(left.Mass, right.Mass);
                    ClashKind glancing = heavier <= lighter * (1f + _settings.GlancingSimilarMassBandFraction)
                        ? ClashKind.BothSlidePast
                        : ClashKind.LighterDeflectsHeavier;
                    Assert.That(Resolve(Glancing, left, right).Kind, Is.EqualTo(glancing), $"{left.Id} against {right.Id}, glancing");
                }
            }
        }

        // --- Blocks: the shield looks at mass too, never at the weapon's name ----------------------------

        [TestCase("sword")]
        [TestCase("spear")]
        public void ASwordOrSpear_SquareOnTheShieldFace_IsFullyBlocked(string id)
        {
            BlockResult result = Block(Weapon(id), Square, MiddleOfTheFace, new ShieldBlockSettings());

            Assert.That(result.AttackStopped, Is.True);
            Assert.That(result.DamageMultiplier, Is.EqualTo(0f));
            Assert.That(result.ShieldHolderStaggered, Is.False);
        }

        [TestCase("sword")]
        [TestCase("spear")]
        public void ASwordOrSpear_OnTheShieldsRim_IsOnlyPartiallyBlocked(string id)
        {
            var settings = new ShieldBlockSettings();

            BlockResult result = Block(Weapon(id), Square, OnTheRim, settings);

            Assert.That(result.AttackStopped, Is.False);
            Assert.That(result.DamageMultiplier, Is.EqualTo(settings.PartialBlockDamageMultiplier));
        }

        [Test]
        public void D21_OnlyTheMaceCountsAsHeavy_SoTheSwordAndSpearNeitherStaggerNorBreakThrough()
        {
            var staggers = new ShieldBlockSettings { HeavyWeapon = HeavyWeaponBlockRule.StaggersHolder };
            var breaksThrough = new ShieldBlockSettings { HeavyWeapon = HeavyWeaponBlockRule.BreaksThrough };

            Assert.That(Block(_mace, Square, MiddleOfTheFace, staggers).ShieldHolderStaggered, Is.True);
            Assert.That(Block(_sword, Square, MiddleOfTheFace, staggers).ShieldHolderStaggered, Is.False);
            Assert.That(Block(_spear, Square, MiddleOfTheFace, staggers).ShieldHolderStaggered, Is.False);

            Assert.That(Block(_mace, Square, MiddleOfTheFace, breaksThrough).AttackStopped, Is.False, "the mace breaks through");
            Assert.That(Block(_sword, Square, MiddleOfTheFace, breaksThrough).AttackStopped, Is.True);
            Assert.That(Block(_spear, Square, MiddleOfTheFace, breaksThrough).AttackStopped, Is.True);
        }

        private WeaponStats Weapon(string id) => id switch
        {
            "rapier" => _rapier,
            "sword" => _sword,
            "spear" => _spear,
            "mace" => _mace,
            _ => throw new System.ArgumentException(id),
        };

        private static ClashParticipant Participant(WeaponStats weapon) =>
            new ClashParticipant(Side.Left, weapon.Mass, weapon.SpeedUnitsPerSecond);

        private static BlockResult Block(WeaponStats attacker, float angle, float facePosition, ShieldBlockSettings settings) =>
            new SettingsShieldBlockModel().Resolve(
                new BlockFacts(angle, facePosition, attacker, attacker.SpeedUnitsPerSecond, GddStartingValues.Shield()), settings);
    }
}
