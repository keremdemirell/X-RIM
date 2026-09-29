using XRim.Core;
using XRim.Rules.Combat;

namespace XRim.Rules.Events
{
    public sealed class WeaponClashEvent : MatchEvent
    {
        public ClashResult Result { get; }
        public Vec2 ContactPointUnits { get; }
        public float ContactAngleDegrees { get; }

        public WeaponClashEvent(SimTime time, ClashResult result, Vec2 contactPointUnits, float contactAngleDegrees)
            : base(time)
        {
            Result = result;
            ContactPointUnits = contactPointUnits;
            ContactAngleDegrees = contactAngleDegrees;
        }
    }
}
