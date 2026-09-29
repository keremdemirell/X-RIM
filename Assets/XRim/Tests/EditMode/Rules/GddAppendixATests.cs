using NUnit.Framework;
using XRim.Rules;
using XRim.Rules.Settings;

namespace XRim.Tests.EditMode.Rules
{
    /// <summary>
    /// The code defaults must equal the GDD Appendix A starting values. Tuned values live in assets, so tuning
    /// never breaks these tests; changing a default here means the GDD changed.
    /// </summary>
    public sealed class GddAppendixATests
    {
        private const float Tolerance = 1e-5f;

        [Test]
        public void MatchDefaults_MatchAppendixA()
        {
            var match = new MatchSettings();
            Assert.That(match.PlanningDurationSeconds, Is.InRange(10f, 12f), "Planning phase 10–12 s");
            Assert.That(match.TurnCap, Is.EqualTo(30), "Turn cap confirmed as 30 by the designer (2026-09-29)");
            Assert.That(match.ExecutionHardCapSeconds, Is.EqualTo(1.5f).Within(Tolerance));
            Assert.That(match.WeaponSwitchLockoutSeconds, Is.EqualTo(1.5f).Within(Tolerance));
        }

        [Test]
        public void DecidedNumbers_AreCodeConstants()
        {
            Assert.That(RuleConstants.IdleTurnsBeforeForfeit, Is.EqualTo(3));
            Assert.That(RuleConstants.SuddenDeathHp, Is.EqualTo(1f));
            Assert.That(RuleConstants.LoadoutSlotCount, Is.EqualTo(3));
        }

        [Test]
        public void CombatDefaults_MatchAppendixA()
        {
            var zones = new HitZoneSettings();
            Assert.That(zones.MultiplierFor(HitZone.Head), Is.EqualTo(2.5f).Within(Tolerance));
            Assert.That(zones.MultiplierFor(HitZone.Torso), Is.EqualTo(1.0f).Within(Tolerance));
            Assert.That(zones.MultiplierFor(HitZone.Arm), Is.EqualTo(0.8f).Within(Tolerance));
            Assert.That(zones.MultiplierFor(HitZone.Leg), Is.EqualTo(0.7f).Within(Tolerance));

            var clash = new ClashSettings();
            Assert.That(clash.HardClashAngleDegrees, Is.EqualTo(30f).Within(Tolerance));
            Assert.That(clash.CrushRatio, Is.EqualTo(1.5f).Within(Tolerance));
            Assert.That(clash.CrushThroughDamageMultiplier, Is.EqualTo(0.7f).Within(Tolerance));
            Assert.That(clash.MassWeight, Is.Not.EqualTo(clash.SpeedWeight), "W_m and W_v must be unequal");

            var damage = new DamageSettings();
            Assert.That(damage.PerHitLimbCapFraction, Is.EqualTo(0.35f).Within(Tolerance));
            Assert.That(damage.OffHandDamageMultiplier, Is.EqualTo(0.8f).Within(Tolerance));

            Assert.That(new RigiditySettings().BendThresholdDegrees, Is.EqualTo(15f).Within(Tolerance));
        }

        [Test]
        public void RosterWeapons_MatchAppendixA()
        {
            WeaponStats rapier = GddStartingValues.Rapier();
            Assert.That(rapier.InkLengthUnits, Is.EqualTo(600f));
            Assert.That(rapier.InkThicknessUnits, Is.EqualTo(10f));
            Assert.That(rapier.Mass, Is.EqualTo(2f));

            WeaponStats mace = GddStartingValues.Mace();
            Assert.That(mace.InkLengthUnits, Is.EqualTo(200f));
            Assert.That(mace.InkThicknessUnits, Is.EqualTo(30f));
            Assert.That(mace.Mass, Is.EqualTo(10f));

            Assert.That(rapier.SpeedUnitsPerSecond, Is.GreaterThan(mace.SpeedUnitsPerSecond), "§9: rapier fast, mace slow");
        }
    }
}
