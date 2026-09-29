using System.Collections.Generic;
using NUnit.Framework;
using XRim.Simulation;
using XRim.Simulation.Settings;

namespace XRim.Tests.EditMode.Simulation
{
    public sealed class SimulationSettingsTests
    {
        private static List<string> Issues(SimulationSettings settings)
        {
            var issues = new List<string>();
            settings.Validate(issues);
            return issues;
        }

        [Test]
        public void Defaults_AreValid()
        {
            Assert.That(Issues(new SimulationSettings()), Is.Empty);
        }

        [Test]
        public void Defaults_KeepBothPt1DecisionsOpen()
        {
            var settings = new SimulationSettings();

            Assert.That(settings.StepRateHz, Is.EqualTo(240), "ARCHITECTURE §6: 240 Hz so the rapier does not tunnel");
            Assert.That(settings.WeaponDriver, Is.EqualTo(WeaponDriverKind.Kinematic));
            Assert.That(settings.Segmentation, Is.EqualTo(RagdollSegmentation.SixBodies));
        }

        [Test]
        public void MotorFasterThanTheStepRateCanHold_IsReported()
        {
            var settings = new SimulationSettings();
            settings.WeaponMotor.FrequencyHz = 60f;

            Assert.That(Issues(settings), Has.Some.Contains("unstable"));
        }

        [Test]
        public void JointLimitsThatExcludeTheRestPose_AreReported()
        {
            var settings = new SimulationSettings();
            settings.Ragdoll.KneeMinDegrees = 10f;
            settings.Ragdoll.KneeMaxDegrees = 20f;

            Assert.That(Issues(settings), Has.Some.Contains("knee"));
        }

        [Test]
        public void NonPositiveMass_IsReported()
        {
            var settings = new SimulationSettings();
            settings.Ragdoll.HeadMass = 0f;

            Assert.That(Issues(settings), Has.Some.Contains("mass"));
        }
    }
}
