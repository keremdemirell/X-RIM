using XRim.Core;
using XRim.Rules.Arena;

namespace XRim.Rules.Events
{
    public sealed class ElectricWallEvent : MatchEvent
    {
        public Side Side { get; }
        public ElectricWallPhase Phase { get; }

        /// <summary>0 for the warning wall.</summary>
        public float HpDamage { get; }

        public ElectricWallEvent(SimTime time, Side side, ElectricWallPhase phase, float hpDamage) : base(time)
        {
            Side = side;
            Phase = phase;
            HpDamage = hpDamage;
        }
    }
}
