using XRim.Core;

namespace XRim.Rules.Events
{
    /// <summary>The dominant arm was severed, so the weapon drops (GDD §12, Decided).</summary>
    public sealed class WeaponDroppedEvent : MatchEvent
    {
        public Side Side { get; }
        public WeaponId Weapon { get; }

        public WeaponDroppedEvent(SimTime time, Side side, WeaponId weapon) : base(time)
        {
            Side = side;
            Weapon = weapon;
        }
    }
}
