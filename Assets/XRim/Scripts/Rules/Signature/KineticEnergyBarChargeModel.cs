using System;
using XRim.Core;
using XRim.Rules.Match;
using XRim.Rules.Settings;

namespace XRim.Rules.Signature
{
    /// <summary>
    /// GDD §8, option 2: a Kinetic Energy Bar filled by the damage a dummy deals and takes, for comeback swings and pacing. The bar runs
    /// from 0 to <see cref="FullBar"/>; a signature move needs it full and spends all of it. Damage taken fills it faster than damage
    /// dealt by default, and the fill rates are placeholders (<see cref="SignatureSettings"/>). The bar has no use cap of its own.
    /// </summary>
    public sealed class KineticEnergyBarChargeModel : ISignatureChargeModel
    {
        /// <summary>The bar's full value: the charge is a fraction of it.</summary>
        public const float FullBar = 1f;

        /// <summary>Absorbs float error: twenty fills of 0.05 must count as full.</summary>
        private const float FullToleranceFraction = 1e-4f;

        public bool IsAvailable(FighterState fighter, SignatureSettings settings)
        {
            Guard.NotNull(fighter, nameof(fighter));
            return fighter.SignatureCharge >= FullBar - FullToleranceFraction;
        }

        public float ChargeFraction(FighterState fighter, SignatureSettings settings)
        {
            Guard.NotNull(fighter, nameof(fighter));
            return XMath.Clamp01(fighter.SignatureCharge / FullBar);
        }

        public void OnDamageTaken(FighterState fighter, float hpDamage, SignatureSettings settings)
        {
            Guard.NotNull(settings, nameof(settings));
            Fill(fighter, hpDamage, settings.BarChargePerHpDamageTaken);
        }

        public void OnDamageDealt(FighterState fighter, float hpDamage, SignatureSettings settings)
        {
            Guard.NotNull(settings, nameof(settings));
            Fill(fighter, hpDamage, settings.BarChargePerHpDamageDealt);
        }

        public void OnUsed(FighterState fighter, SignatureSettings settings)
        {
            Guard.NotNull(fighter, nameof(fighter));
            fighter.SignatureCharge = 0f;
            fighter.SignatureUses++;
        }

        private static void Fill(FighterState fighter, float hpDamage, float perHp)
        {
            Guard.NotNull(fighter, nameof(fighter));
            if (hpDamage <= 0f || perHp <= 0f) return;
            fighter.SignatureCharge = Math.Min(FullBar, fighter.SignatureCharge + hpDamage * perHp);
        }
    }
}
