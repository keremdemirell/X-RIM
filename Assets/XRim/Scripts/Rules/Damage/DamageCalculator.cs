using System;
using System.Collections.Generic;
using XRim.Rules.Match;
using XRim.Rules.Settings;

namespace XRim.Rules.Damage
{
    /// <summary>
    /// Damage = BaseDamage(weapon) × ZoneMultiplier × Modifiers (GDD §11, Decided). Designer-set base
    /// damage, never physics energy (kinetic-energy damage is Rejected). Applies the per-hit limb cap.
    /// </summary>
    public sealed class DamageCalculator
    {
        public DamageResult Calculate(DamageContext context, IReadOnlyList<IDamageModifier> modifiers,
            FighterState victim, RulesSettings settings)
        {
            // Placeholder: architecture setup only. Gameplay implementation comes later.
            throw new NotImplementedException("DamageCalculator.Calculate is not implemented yet.");
        }
    }
}
