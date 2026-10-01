using System;
using System.Collections.Generic;
using XRim.Core.Gdd;
using XRim.Simulation.Settings;

namespace XRim.Simulation
{
    /// <summary>
    /// Technical tunables for the execution simulation. These are not GDD values; they exist so the GDD's
    /// timing rules (millisecond time-to-impact, 1.5 s hard cap) can be met and tuned for feel and device cost.
    /// </summary>
    [Serializable]
    public sealed class SimulationSettings
    {
        /// <summary>A spring-damper stepped at a fixed rate stays stable only well below that rate.</summary>
        private const int MinStepsPerMotorPeriod = 8;

        /// <summary>Fixed physics steps per second. High enough that a thin, fast rapier does not tunnel.</summary>
        public int StepRateHz = 240;

        /// <summary>Physics counts as settled when every body is slower than this...</summary>
        public float SettleLinearSpeedUnitsPerSecond = 5f;

        public float SettleAngularSpeedDegreesPerSecond = 10f;

        /// <summary>...for this many consecutive steps (GDD §3: execution ends when paths finish and physics settles).</summary>
        public int SettleStepsRequired = 6;

        /// <summary>1 = record a pose every step. Higher values shrink recordings (e.g. for network transfer).</summary>
        public int RecordEveryNthStep = 1;

        /// <summary>D1: kinematic path following or a motor chasing the path. Switchable live in the feel spike.</summary>
        [Placeholder("D1: the designer picks the weapon driver at PT1")]
        public WeaponDriverKind WeaponDriver = WeaponDriverKind.Kinematic;

        /// <summary>D2: six or ten physics bodies per dummy. Switchable live in the feel spike.</summary>
        [Placeholder("D2: the designer picks the ragdoll segmentation at PT1")]
        public RagdollSegmentation Segmentation = RagdollSegmentation.SixBodies;

        /// <summary>
        /// Downward pull on every body. Set per body, because Unity's 2D gravity is global and the hidden
        /// simulation scene must not depend on it.
        /// </summary>
        [Placeholder("9.81 m/s² at the ~2.5 mm per arena unit estimate; the real scale is TBD (§18)")]
        public float GravityUnitsPerSecondSquared = 3924f;

        public WeaponMotorSettings WeaponMotor = new WeaponMotorSettings();
        public RootDriveSettings RootDrive = new RootDriveSettings();
        public RagdollSettings Ragdoll = new RagdollSettings();

        /// <summary>Appends a message for every value that cannot work. Placeholders are not errors.</summary>
        public void Validate(ICollection<string> issues)
        {
            if (StepRateHz <= 0) issues.Add("Simulation: step rate must be positive.");
            if (SettleStepsRequired <= 0) issues.Add("Simulation: settle steps must be positive.");
            if (RecordEveryNthStep <= 0) issues.Add("Simulation: record interval must be at least 1.");
            if (GravityUnitsPerSecondSquared < 0f) issues.Add("Simulation: gravity must not be negative.");
            ValidateMotor(issues);
            ValidateRootDrive(issues);
            ValidateRagdoll(issues);
        }

        private void ValidateMotor(ICollection<string> issues)
        {
            WeaponMotorSettings motor = WeaponMotor;
            float maxFrequencyHz = (float)StepRateHz / MinStepsPerMotorPeriod;
            if (motor.FrequencyHz <= 0f || motor.AngularFrequencyHz <= 0f)
                issues.Add("Simulation: motor frequencies must be positive.");
            if (motor.FrequencyHz > maxFrequencyHz || motor.AngularFrequencyHz > maxFrequencyHz)
                issues.Add($"Simulation: motor frequencies above {maxFrequencyHz:0.#} Hz are unstable at {StepRateHz} Hz.");
            if (motor.DampingRatio < 0f || motor.AngularDampingRatio < 0f)
                issues.Add("Simulation: motor damping must not be negative.");
            if (motor.MaxAccelerationUnitsPerSecondSquared < 0f || motor.MaxAngularAccelerationDegreesPerSecondSquared < 0f)
                issues.Add("Simulation: motor strength must not be negative (0 = unlimited).");
        }

        private void ValidateRootDrive(ICollection<string> issues)
        {
            RootDriveSettings root = RootDrive;
            if (root.MaxAccelerationUnitsPerSecondSquared <= 0f || root.MaxAngularAccelerationDegreesPerSecondSquared <= 0f)
                issues.Add("Simulation: root drive strength must be positive.");
            if (root.CorrectionFraction < 0f || root.CorrectionFraction > 1f)
                issues.Add("Simulation: root drive correction must be in [0, 1].");
        }

        private void ValidateRagdoll(ICollection<string> issues)
        {
            RagdollSettings body = Ragdoll;
            if (body.HeadDiameterUnits <= 0f || body.TorsoWidthUnits <= 0f || body.TorsoHeightUnits <= 0f ||
                body.ArmWidthUnits <= 0f || body.LegWidthUnits <= 0f || body.LegLengthUnits <= 0f)
                issues.Add("Ragdoll: every size must be positive.");
            if (body.UpperSegmentFraction <= 0f || body.UpperSegmentFraction >= 1f)
                issues.Add("Ragdoll: the upper segment fraction must be between 0 and 1.");
            if (body.HeadMass <= 0f || body.TorsoMass <= 0f || body.ArmMass <= 0f || body.LegMass <= 0f)
                issues.Add("Ragdoll: every mass must be positive.");
            AddLimitIssue(issues, "neck", body.NeckMinDegrees, body.NeckMaxDegrees);
            AddLimitIssue(issues, "shoulder", body.ShoulderMinDegrees, body.ShoulderMaxDegrees);
            AddLimitIssue(issues, "elbow", body.ElbowMinDegrees, body.ElbowMaxDegrees);
            AddLimitIssue(issues, "hip", body.HipMinDegrees, body.HipMaxDegrees);
            AddLimitIssue(issues, "knee", body.KneeMinDegrees, body.KneeMaxDegrees);
            if (body.JointServoGainPerSecond < 0f || body.JointServoMaxAngularAccelerationDegreesPerSecondSquared < 0f)
                issues.Add("Ragdoll: pose holding must not be negative.");
            if (body.GuardHandReachFraction < 0f || body.GuardHandReachFraction > 1f)
                issues.Add("Ragdoll: the guard hand reach must be between 0 and 1.");
            float maxSpringHz = (float)StepRateHz / MinStepsPerMotorPeriod;
            if (body.WeaponArmFollowFrequencyHz <= 0f || body.WeaponArmFollowFrequencyHz > maxSpringHz)
                issues.Add($"Ragdoll: the weapon arm follow frequency must be in (0, {maxSpringHz:0.#}] Hz at {StepRateHz} Hz.");
            if (body.WeaponArmFollowDampingRatio < 0f || body.WeaponArmFollowMaxAccelerationUnitsPerSecondSquared <= 0f)
                issues.Add("Ragdoll: the weapon arm follow damping must not be negative and its strength must be positive.");
        }

        private static void AddLimitIssue(ICollection<string> issues, string joint, float minDegrees, float maxDegrees)
        {
            if (minDegrees > 0f || maxDegrees < 0f)
                issues.Add($"Ragdoll: the {joint} limits must include the rest angle 0.");
            if (minDegrees > maxDegrees)
                issues.Add($"Ragdoll: the {joint} minimum must not exceed its maximum.");
        }
    }
}
