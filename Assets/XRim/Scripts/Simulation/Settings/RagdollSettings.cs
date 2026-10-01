using System;
using XRim.Core.Gdd;

namespace XRim.Simulation.Settings
{
    /// <summary>
    /// The placeholder dummy's body, in arena units. Sizes are baked when the ragdoll prefab is built (rebuild it after
    /// changing them); masses, joint limits and pose holding are applied every time a turn loads, so they can be tuned
    /// live. The shoulder position and the arm length come from <c>PathSettings</c>, so the ragdoll's reach always
    /// matches the §6 reach limit.
    /// Joint angles are relative to the rest pose (arms and legs hanging straight down, facing +X). Positive angles
    /// turn counter-clockwise, which swings a limb forward, toward the opponent.
    /// </summary>
    [Serializable]
    public sealed class RagdollSettings
    {
        [Placeholder("§2/§18 dummy proportions are not given")]
        public float HeadDiameterUnits = 50f;

        /// <summary>The torso's pivot is the pelvis (the origin of the torso frame paths are drawn in).</summary>
        [Placeholder("§2/§18 dummy proportions are not given")]
        public float TorsoWidthUnits = 60f;

        [Placeholder("§2/§18 dummy proportions are not given")]
        public float TorsoHeightUnits = 120f;

        [Placeholder("§2/§18 dummy proportions are not given")]
        public float ArmWidthUnits = 24f;

        [Placeholder("§2/§18 dummy proportions are not given")]
        public float LegWidthUnits = 28f;

        /// <summary>Pelvis to sole. The dummy stands with its pelvis this high above the floor.</summary>
        [Placeholder("§2/§18 dummy proportions are not given")]
        public float LegLengthUnits = 180f;

        /// <summary>With ten bodies: the upper segment's share of an arm or leg (the rest is the lower segment).</summary>
        [Placeholder("D2 spike: where limbs split, tuned at PT1")]
        public float UpperSegmentFraction = 0.5f;

        [Placeholder("Session 02 spike: body masses, tuned at PT1")]
        public float HeadMass = 2f;

        [Placeholder("Session 02 spike: body masses, tuned at PT1")]
        public float TorsoMass = 10f;

        /// <summary>Per arm. With ten bodies it is split between the segments by length.</summary>
        [Placeholder("Session 02 spike: body masses, tuned at PT1")]
        public float ArmMass = 2f;

        /// <summary>Per leg. With ten bodies it is split between the segments by length.</summary>
        [Placeholder("Session 02 spike: body masses, tuned at PT1")]
        public float LegMass = 4f;

        [Placeholder("Session 02 spike: joint limits, tuned at PT1")]
        public float NeckMinDegrees = -30f;

        [Placeholder("Session 02 spike: joint limits, tuned at PT1")]
        public float NeckMaxDegrees = 30f;

        /// <summary>Wide on purpose: the weapon carries the weapon arm anywhere the path goes, and a limit would fight it.</summary>
        [Placeholder("Session 02 spike: joint limits, tuned at PT1")]
        public float ShoulderMinDegrees = -120f;

        [Placeholder("Session 02 spike: joint limits, tuned at PT1")]
        public float ShoulderMaxDegrees = 220f;

        /// <summary>Ten bodies only.</summary>
        [Placeholder("Session 02 spike: joint limits, tuned at PT1")]
        public float ElbowMinDegrees = 0f;

        [Placeholder("Session 02 spike: joint limits, tuned at PT1")]
        public float ElbowMaxDegrees = 140f;

        [Placeholder("Session 02 spike: joint limits, tuned at PT1")]
        public float HipMinDegrees = -30f;

        [Placeholder("Session 02 spike: joint limits, tuned at PT1")]
        public float HipMaxDegrees = 90f;

        /// <summary>Ten bodies only. The knee bends backward (clockwise).</summary>
        [Placeholder("Session 02 spike: joint limits, tuned at PT1")]
        public float KneeMinDegrees = -130f;

        [Placeholder("Session 02 spike: joint limits, tuned at PT1")]
        public float KneeMaxDegrees = 0f;

        /// <summary>
        /// Pose holding: each joint's motor turns back toward the rest angle at this many degrees per second for every
        /// degree it is bent away. 0 = limp limbs.
        /// </summary>
        [Placeholder("Session 02 spike: pose holding, tuned at PT1")]
        public float JointServoGainPerSecond = 20f;

        /// <summary>Strongest turn a joint motor can give (the motor torque is this times the limb's inertia).</summary>
        [Placeholder("Session 02 spike: pose holding strength, tuned at PT1")]
        public float JointServoMaxAngularAccelerationDegreesPerSecondSquared = 36000f;

        /// <summary>False = the weapon arm's joint motors are off, so only the hand spring below moves it.</summary>
        public bool ServoWeaponArm;

        /// <summary>
        /// The weapon arm follows the weapon: a spring pulls its hand onto the blade, where the arm can reach. The weapon
        /// feels nothing back, so the arm can never block or yank it. Higher = the arm sticks to the weapon more tightly.
        /// </summary>
        [Placeholder("Session 02 spike: how tightly the weapon arm follows the weapon, tuned at PT1")]
        public float WeaponArmFollowFrequencyHz = 20f;

        [Placeholder("Session 02 spike: weapon arm follow damping, tuned at PT1")]
        public float WeaponArmFollowDampingRatio = 1f;

        /// <summary>The strongest pull on the hand. Lower = a hit on the arm can knock it off the weapon for a moment.</summary>
        [Placeholder("Session 02 spike: weapon arm follow strength, tuned at PT1")]
        public float WeaponArmFollowMaxAccelerationUnitsPerSecondSquared = 200000f;

        /// <summary>
        /// The guard stance a dummy stands in before its first turn ("en garde"): the weapon points this way from the
        /// shoulder (0 = straight at the opponent, negative = down). Later turns start from wherever the last one ended.
        /// </summary>
        [Placeholder("Session 02 spike: guard stance, tuned at PT1")]
        public float GuardAngleDegrees = -20f;

        /// <summary>How far the weapon hand is pushed out in the guard stance, as a share of the arm's length (0..1).</summary>
        [Placeholder("Session 02 spike: guard stance, tuned at PT1")]
        public float GuardHandReachFraction = 0.5f;
    }
}
