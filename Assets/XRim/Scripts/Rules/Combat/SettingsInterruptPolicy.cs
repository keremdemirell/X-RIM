using System;
using XRim.Core;

namespace XRim.Rules.Combat
{
    /// <summary>
    /// The default <see cref="IInterruptPolicy"/>: a weapon with swing armour (D16, off by default) keeps its attack; otherwise
    /// <c>DamageSettings.InterruptRule</c> decides (D15, designer 2026-10-03: weapon arm or head), so every GDD option can be
    /// tried from the tuning panel. "Above a threshold" means strictly above, as the GDD words it.
    /// </summary>
    public sealed class SettingsInterruptPolicy : IInterruptPolicy
    {
        public bool Interrupts(InterruptCheck check)
        {
            Guard.NotNull(check.Settings, nameof(check.Settings));
            if (check.VictimWeapon != null && check.VictimWeapon.HasSwingArmour) return false;

            switch (check.Settings.InterruptRule)
            {
                case InterruptRule.AnyHit:
                    return true;
                case InterruptRule.WeaponArmOrHead:
                    return check.Hit.Part == BodyPart.Head || check.HitsVictimWeaponArm;
                case InterruptRule.AboveDamageThreshold:
                    return check.Damage.Damage > check.Settings.InterruptDamageThreshold;
                default:
                    throw new ArgumentOutOfRangeException(nameof(check), check.Settings.InterruptRule, "Unknown interrupt rule.");
            }
        }
    }
}
