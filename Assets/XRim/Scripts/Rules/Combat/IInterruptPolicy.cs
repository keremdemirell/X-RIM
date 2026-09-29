using XRim.Core.Gdd;
using XRim.Rules.Settings;

namespace XRim.Rules.Combat
{
    /// <summary>
    /// Whether a landed hit cancels the victim's own attack. The priority rule (first weapon to a valid
    /// hitbox lands; a dummy hit first has its attack interrupted) is Decided in GDD §9; which hits
    /// qualify is not. Options: any clean hit, only weapon arm or head, or above a damage threshold.
    /// </summary>
    [GddTbd("§9", "Which hits interrupt an attack")]
    [GddTbd("§9", "Do heavy weapons resist interruption?")]
    public interface IInterruptPolicy
    {
        bool Interrupts(HitFacts hit, float hpDamage, WeaponStats victimWeapon, bool victimSwingStarted);
    }
}
