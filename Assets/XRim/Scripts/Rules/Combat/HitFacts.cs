using XRim.Core;

namespace XRim.Rules.Combat
{
    /// <summary>A weapon reaching a valid hitbox, as reported by the simulation (GDD §9, §11).</summary>
    public readonly struct HitFacts
    {
        public Side Attacker { get; }
        public Side Victim => Attacker.Opponent();
        public BodyPart Part { get; }
        public WeaponId Weapon { get; }

        /// <summary>Time-to-impact on the simulation clock: distance along path / weapon speed (GDD §9).</summary>
        public SimTime Time { get; }

        /// <summary>True when the attacker holds the weapon in its off hand after losing the dominant arm (GDD §12).</summary>
        public bool IsOffHand { get; }

        public HitFacts(Side attacker, BodyPart part, WeaponId weapon, SimTime time, bool isOffHand)
        {
            Attacker = attacker;
            Part = part;
            Weapon = weapon;
            Time = time;
            IsOffHand = isOffHand;
        }
    }
}
