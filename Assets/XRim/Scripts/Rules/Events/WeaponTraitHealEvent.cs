using XRim.Core;

namespace XRim.Rules.Events
{
    /// <summary>
    /// A weapon trait gave HP back (GDD §15, lifesteal): it lands at the end of the instant of the hit that completed the count, so
    /// the HP bar rises there. Only a weapon that carries the trait, with its flag on, ever makes one.
    /// </summary>
    public sealed class WeaponTraitHealEvent : MatchEvent
    {
        public Side Side { get; }

        /// <summary>The trait, as the weapon lists it (<see cref="XRim.Rules.Damage.WeaponTraitIds"/>).</summary>
        public string TraitId { get; }

        /// <summary>The HP actually gained: never above the maximum.</summary>
        public float HpRecovered { get; }

        public WeaponTraitHealEvent(SimTime time, Side side, string traitId, float hpRecovered) : base(time)
        {
            Side = side;
            TraitId = traitId;
            HpRecovered = hpRecovered;
        }
    }
}
