using System;
using XRim.Core;
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
        /// <summary>A limb's durability: arms and legs have their own values (§11). Head and torso cannot be severed.</summary>
        public float DurabilityOf(BodyPart part, DamageSettings settings)
        {
            Guard.NotNull(settings, nameof(settings));
            if (part.IsArm()) return settings.ArmDurability;
            if (part.IsLeg()) return settings.LegDurability;
            throw new ArgumentException($"{part} has no durability: only arms and legs can be severed (GDD §11).", nameof(part));
        }

        /// <summary>The damage one hit adds to a limb: its damage, at most the per-hit cap of the limb's durability.</summary>
        public float CapLimbDamage(float incomingDamage, float limbDurability, DamageSettings settings)
        {
            Guard.NotNull(settings, nameof(settings));
            return Math.Max(0f, Math.Min(incomingDamage, limbDurability * settings.PerHitLimbCapFraction));
        }
    }
}
