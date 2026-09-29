using XRim.Rules.Combat;
using XRim.Rules.Settings;

namespace XRim.Rules.Damage
{
    /// <summary>Inputs to one damage calculation (GDD §11).</summary>
    public sealed class DamageContext
    {
        public HitFacts Hit { get; }
        public WeaponStats Weapon { get; }
        public float ZoneMultiplier { get; }

        public DamageContext(HitFacts hit, WeaponStats weapon, float zoneMultiplier)
        {
            Hit = hit;
            Weapon = weapon;
            ZoneMultiplier = zoneMultiplier;
        }
    }
}
