using System.Collections.Generic;
using NUnit.Framework;
using XRim.Rules;
using XRim.Rules.Settings;

namespace XRim.Tests.EditMode.Rules
{
    /// <summary>
    /// The roster orderings <c>RulesSettings.Validate</c> keeps for the five weapons (Session 14): the spear has the longest
    /// reach (§6), the shield's damage is very low (§7), the rapier bends easier than the spear (§6), the rapier is faster
    /// and hits softer than the mace (§6, §9) and no weapon is strictly stronger than another (§16).
    /// </summary>
    public sealed class RosterValidationTests
    {
        private RulesSettings _rules;

        [SetUp]
        public void SetUp()
        {
            _rules = GddStartingValues.CreateRulesSettings();
        }

        private List<string> Issues()
        {
            var issues = new List<string>();
            _rules.Validate(issues);
            return issues;
        }

        private WeaponStats Weapon(WeaponId id) => _rules.FindWeapon(id);

        /// <summary>True when one issue contains every one of these texts.</summary>
        private bool HasIssueWith(params string[] parts) =>
            Issues().Exists(issue => System.Array.TrueForAll(parts, part => issue.Contains(part)));

        [Test]
        public void TheStartingRoster_KeepsEveryOrdering()
        {
            Assert.That(Issues(), Is.Empty);
        }

        // --- §6: the spear has the longest reach --------------------------------------------------

        [Test]
        public void ASpearNoLongerThanTheSword_IsReported()
        {
            Weapon(WeaponIds.Spear).LengthUnits = Weapon(WeaponIds.Sword).LengthUnits;

            Assert.That(HasIssueWith("the spear the longest reach", "'sword'"), Is.True, string.Join(" | ", Issues()));
        }

        [Test]
        public void ASwordLongerThanTheSpear_IsReported()
        {
            Weapon(WeaponIds.Sword).LengthUnits = Weapon(WeaponIds.Spear).LengthUnits + 1f;

            Assert.That(HasIssueWith("the spear the longest reach", "'sword'"), Is.True, string.Join(" | ", Issues()));
        }

        [Test]
        public void TheShieldAndTheClub_DoNotCountAsReachForTheSpear()
        {
            Weapon(WeaponIds.Shield).LengthUnits = 900f;
            Weapon(WeaponIds.SeveredLimb).LengthUnits = 900f;

            Assert.That(Issues(), Has.None.Contains("longest reach"));
        }

        // --- §7: the shield's damage is very low --------------------------------------------------

        [Test]
        public void AShieldThatHitsAsHardAsTheRapier_IsReported()
        {
            Weapon(WeaponIds.Shield).BaseDamage = Weapon(WeaponIds.Rapier).BaseDamage;

            Assert.That(HasIssueWith("very low damage", "'rapier'"), Is.True, string.Join(" | ", Issues()));
        }

        // --- §6: the rapier tolerates bends better than the spear -----------------------------------

        [Test]
        public void ARapierWithATinyBendPenalty_IsFine()
        {
            WeaponStats rapier = Weapon(WeaponIds.Rapier);
            rapier.Rigidity.Enabled = true;
            rapier.Rigidity.BendCostK = 0.002f;
            rapier.Rigidity.BreakAngleDegrees = 150f;

            Assert.That(Issues(), Is.Empty);
        }

        [Test]
        public void ARapierThatBendsMoreThanTheSpear_IsReported()
        {
            WeaponStats rapier = Weapon(WeaponIds.Rapier);
            rapier.Rigidity.Enabled = true;
            rapier.Rigidity.BendCostK = Weapon(WeaponIds.Spear).Rigidity.BendCostK * 2f;

            Assert.That(Issues(), Has.Some.Contains("rapier almost no bend penalty"));
        }

        [Test]
        public void ARapierThatBreaksAtAGentlerTurnThanTheSpear_IsReported()
        {
            WeaponStats rapier = Weapon(WeaponIds.Rapier);
            rapier.Rigidity.Enabled = true;
            rapier.Rigidity.BendCostK = 0f;
            rapier.Rigidity.BreakAngleDegrees = Weapon(WeaponIds.Spear).Rigidity.BreakAngleDegrees - 1f;

            Assert.That(Issues(), Has.Some.Contains("very tolerant of curves"));
        }

        [Test]
        public void WithTheSpearsRigidityCut_ARapierThatStillBends_IsReported()
        {
            Weapon(WeaponIds.Spear).Rigidity.Enabled = false;
            WeaponStats rapier = Weapon(WeaponIds.Rapier);
            rapier.Rigidity.Enabled = true;
            rapier.Rigidity.BendCostK = 0.002f;

            Assert.That(Issues(), Has.Some.Contains("rapier almost no bend penalty"));
        }

        [Test]
        public void WithTheSpearsRigidityCut_ARapierWithoutRigidity_IsFine()
        {
            Weapon(WeaponIds.Spear).Rigidity.Enabled = false;

            Assert.That(Issues(), Is.Empty);
        }

        // --- §6, §9: the rapier is faster and hits softer than the mace -------------------------------

        [Test]
        public void ARapierNoFasterThanTheMace_IsReported()
        {
            Weapon(WeaponIds.Rapier).SpeedUnitsPerSecond = Weapon(WeaponIds.Mace).SpeedUnitsPerSecond;

            Assert.That(Issues(), Has.Some.Contains("rapier fast, mace slow"));
        }

        [Test]
        public void ARapierThatHitsAsHardAsTheMace_IsReported()
        {
            Weapon(WeaponIds.Rapier).BaseDamage = Weapon(WeaponIds.Mace).BaseDamage;

            Assert.That(Issues(), Has.Some.Contains("rapier low damage"));
        }

        // --- §16: no weapon is strictly stronger -------------------------------------------------

        [Test]
        public void AWeaponThatWinsEveryNumber_IsReported()
        {
            // The rapier is already faster, longer and has more ink than the sword; with the sword's damage and mass it wins them all.
            WeaponStats rapier = Weapon(WeaponIds.Rapier);
            WeaponStats sword = Weapon(WeaponIds.Sword);
            rapier.BaseDamage = sword.BaseDamage;
            rapier.Mass = sword.Mass;

            Assert.That(Issues(), Has.Some.Contains("'rapier' is strictly stronger than 'sword'"));
        }

        [Test]
        public void AWeaponThatLosesOneNumber_IsASidegrade()
        {
            WeaponStats rapier = Weapon(WeaponIds.Rapier);
            WeaponStats sword = Weapon(WeaponIds.Sword);
            rapier.BaseDamage = sword.BaseDamage;
            rapier.Mass = sword.Mass - 1f;

            Assert.That(Issues(), Has.None.Contains("strictly stronger"));
        }

        [Test]
        public void TwoWeaponsWithTheSameNumbers_AreNotStrictlyStrongerThanEachOther()
        {
            WeaponStats copy = GddStartingValues.Sword();
            copy.Id = "sword_copy";
            _rules.Weapons.Add(copy);

            Assert.That(Issues(), Has.None.Contains("strictly stronger"));
        }

        [Test]
        public void TheShieldAndTheClub_AreNotJudgedAgainstTheWeapons()
        {
            // The club is a stopgap item with its own rules (§12); the shield trades damage for blocking (§7).
            Weapon(WeaponIds.SeveredLimb).SpeedUnitsPerSecond = 5000f;
            Weapon(WeaponIds.SeveredLimb).InkLengthUnits = 5000f;
            Weapon(WeaponIds.Shield).Mass = 50f;

            Assert.That(Issues(), Has.None.Contains("strictly stronger"));
        }
    }
}
