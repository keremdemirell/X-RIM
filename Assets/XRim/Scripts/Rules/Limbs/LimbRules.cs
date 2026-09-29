using System;
using XRim.Rules.Settings;

namespace XRim.Rules.Limbs
{
    /// <summary>
    /// Limb durability (GDD §11, Decided): arm and leg hits add to the limb's own damage, which never heals.
    /// One hit adds at most PerHitLimbCapFraction of durability, so severing needs 3+ hits and cannot happen
    /// on the first turn. At zero durability the simulation breaks the joint by logic, never breakForce (§18).
    /// </summary>
    public sealed class LimbRules
    {
        public float CapLimbDamage(float incomingDamage, float limbDurability, DamageSettings settings)
        {
            // Placeholder: architecture setup only. Gameplay implementation comes later.
            throw new NotImplementedException("LimbRules.CapLimbDamage is not implemented yet.");
        }
    }
}
