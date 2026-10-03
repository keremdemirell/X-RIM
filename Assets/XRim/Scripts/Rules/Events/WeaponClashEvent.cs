using XRim.Core;
using XRim.Rules.Combat;

namespace XRim.Rules.Events
{
    /// <summary>
    /// Two weapons met and the two-stage model decided what happened (GDD §10). It carries the angle, both powers and the
    /// outcome for the gizmos, and the sparks for the feel layer.
    /// </summary>
    public sealed class WeaponClashEvent : MatchEvent
    {
        public ClashResult Result { get; }
        public Vec2 ContactPointUnits { get; }
        public float ContactAngleDegrees => Result.ContactAngleDegrees;

        public WeaponClashEvent(SimTime time, ClashResult result, Vec2 contactPointUnits) : base(time)
        {
            Result = result;
            ContactPointUnits = contactPointUnits;
        }
    }
}
