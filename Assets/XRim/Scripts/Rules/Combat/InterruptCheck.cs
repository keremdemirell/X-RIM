using XRim.Rules.Damage;
using XRim.Rules.Settings;

namespace XRim.Rules.Combat
{
    /// <summary>
    /// What an <see cref="IInterruptPolicy"/> decides from: a hit that landed while the victim's own attack was travelling its
    /// path. A killing hit is never asked about: it always stops the dead dummy's attack.
    /// </summary>
    public readonly struct InterruptCheck
    {
        public HitFacts Hit { get; }
        public DamageResult Damage { get; }

        /// <summary>The victim's weapon; never null (a dummy holding nothing has no attack to interrupt).</summary>
        public WeaponStats VictimWeapon { get; }

        /// <summary>The hit is on the arm that holds the victim's weapon: the dominant arm, or the off arm once it is lost (§12).</summary>
        public bool HitsVictimWeaponArm { get; }

        public DamageSettings Settings { get; }

        public InterruptCheck(HitFacts hit, DamageResult damage, WeaponStats victimWeapon, bool hitsVictimWeaponArm, DamageSettings settings)
        {
            Hit = hit;
            Damage = damage;
            VictimWeapon = victimWeapon;
            HitsVictimWeaponArm = hitsVictimWeaponArm;
            Settings = settings;
        }
    }
}
