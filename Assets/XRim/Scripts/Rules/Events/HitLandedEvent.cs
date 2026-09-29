using XRim.Core;
using XRim.Rules.Combat;
using XRim.Rules.Damage;

namespace XRim.Rules.Events
{
    public sealed class HitLandedEvent : MatchEvent
    {
        public HitFacts Hit { get; }
        public DamageResult Damage { get; }

        public HitLandedEvent(SimTime time, HitFacts hit, DamageResult damage) : base(time)
        {
            Hit = hit;
            Damage = damage;
        }
    }
}
