using System;
using XRim.Core.Gdd;

namespace XRim.Simulation.Settings
{
    /// <summary>
    /// Gains of the motor weapon driver: a spring-damper that chases the path target. Expressed as a frequency and a
    /// damping ratio so the feel stays the same when a weapon's mass changes. Accelerations are mass-free; the physics
    /// world multiplies by the body's mass and inertia.
    /// </summary>
    [Serializable]
    public sealed class WeaponMotorSettings
    {
        /// <summary>How stiffly the weapon is pulled onto its path. Higher = tighter tracking, harder impacts.</summary>
        [Placeholder("Session 02 spike: motor stiffness, tuned at PT1")]
        public float FrequencyHz = 12f;

        /// <summary>1 = critically damped (no overshoot). Below 1 the weapon wobbles around its path.</summary>
        [Placeholder("Session 02 spike: motor damping, tuned at PT1")]
        public float DampingRatio = 1f;

        [Placeholder("Session 02 spike: motor rotation stiffness, tuned at PT1")]
        public float AngularFrequencyHz = 12f;

        [Placeholder("Session 02 spike: motor rotation damping, tuned at PT1")]
        public float AngularDampingRatio = 1f;

        /// <summary>Strongest pull the motor can give. 0 = unlimited. Lower = impacts knock the weapon off its path more.</summary>
        [Placeholder("Session 02 spike: motor strength, tuned at PT1")]
        public float MaxAccelerationUnitsPerSecondSquared = 60000f;

        /// <summary>Strongest turn the motor can give. 0 = unlimited.</summary>
        [Placeholder("Session 02 spike: motor rotation strength, tuned at PT1")]
        public float MaxAngularAccelerationDegreesPerSecondSquared = 60000f;
    }
}
