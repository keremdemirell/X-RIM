using XRim.Rules.Combat;
using XRim.Rules.Match;
using XRim.Rules.Settings;

namespace XRim.Rules.Damage
{
    /// <summary>A hit that just landed, as a trait sees it (GDD §15): who landed it, with what, and what it did.</summary>
    public readonly struct TraitHitContext
    {
        public HitFacts Hit { get; }

        /// <summary>The dummy that landed the hit. A trait keeps its counters on it, never on itself.</summary>
        public FighterState Attacker { get; }

        public WeaponStats Weapon { get; }
        public DamageResult Damage { get; }
        public TraitSettings Settings { get; }

        public TraitHitContext(HitFacts hit, FighterState attacker, WeaponStats weapon, DamageResult damage, TraitSettings settings)
        {
            Hit = hit;
            Attacker = attacker;
            Weapon = weapon;
            Damage = damage;
            Settings = settings;
        }
    }
}
