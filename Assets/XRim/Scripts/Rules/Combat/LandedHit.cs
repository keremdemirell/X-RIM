using XRim.Rules.Damage;

namespace XRim.Rules.Combat
{
    /// <summary>A hit the rules accepted, with what the simulation applies for it: the knock, and the attacker's weapon slowing or stopping.</summary>
    public sealed class LandedHit
    {
        public HitFacts Hit { get; }
        public DamageResult Damage { get; }

        /// <summary>The share of its path speed the weapon had when it hit (1 for its first hit): scales the knock (D26).</summary>
        public float SpeedFractionAtHit { get; }

        /// <summary>The share it continues its path at (D26). Meaningless when <see cref="StopsWeapon"/>.</summary>
        public float SpeedFractionAfter { get; }

        /// <summary>The weapon stops at this hit with a recoil: its last allowed hit, or no speed left (D26).</summary>
        public bool StopsWeapon { get; }

        public bool KillsVictim { get; }

        public LandedHit(HitFacts hit, DamageResult damage, float speedFractionAtHit, float speedFractionAfter, bool stopsWeapon,
            bool killsVictim)
        {
            Hit = hit;
            Damage = damage;
            SpeedFractionAtHit = speedFractionAtHit;
            SpeedFractionAfter = speedFractionAfter;
            StopsWeapon = stopsWeapon;
            KillsVictim = killsVictim;
        }
    }
}
