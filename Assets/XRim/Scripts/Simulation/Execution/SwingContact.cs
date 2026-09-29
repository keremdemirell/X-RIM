using XRim.Core;
using XRim.Simulation.Physics;

namespace XRim.Simulation.Execution
{
    /// <summary>A contact measured during a swing: what touched, when exactly, how far along the path, and at what angle.</summary>
    public sealed class SwingContact
    {
        public ContactFacts Facts { get; }

        /// <summary>The physics step after which the engine reported the contact (1 = the first step).</summary>
        public int ReportedStep { get; }

        /// <summary>
        /// When the contact happened. For a weapon, refined inside the step from the blade's motion (GDD §9:
        /// time-to-impact to the millisecond); otherwise the reporting step's time.
        /// </summary>
        public SimTime Time { get; }

        /// <summary>The side whose weapon made the contact; null for body-to-body contacts.</summary>
        public Side? WeaponSide { get; }

        /// <summary>That weapon's distance along its path at <see cref="Time"/>: d = v·t (GDD §9). 0 without a weapon path.</summary>
        public float PathDistanceUnits { get; }

        /// <summary>GDD §10 stage 1: 0° = sliding, 90° = square impact.</summary>
        public float ContactAngleDegrees { get; }

        public float RelativeSpeedUnitsPerSecond => Facts.RelativeVelocityUnitsPerSecond.Length;

        public SwingContact(ContactFacts facts, int reportedStep, SimTime time, Side? weaponSide, float pathDistanceUnits,
            float contactAngleDegrees)
        {
            Facts = facts;
            ReportedStep = reportedStep;
            Time = time;
            WeaponSide = weaponSide;
            PathDistanceUnits = pathDistanceUnits;
            ContactAngleDegrees = contactAngleDegrees;
        }
    }
}
