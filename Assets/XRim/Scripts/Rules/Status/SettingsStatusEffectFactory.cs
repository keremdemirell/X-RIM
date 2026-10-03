using System;
using XRim.Core;
using XRim.Rules.Settings;

namespace XRim.Rules.Status
{
    /// <summary>
    /// The default <see cref="IStatusEffectFactory"/>: the effect <see cref="DamageSettings.StunEffect"/> names, for the next turn
    /// only, so all three GDD options can be tried from the tuning panel. A stagger shares the stun's definition (§10 proposal).
    /// </summary>
    public sealed class SettingsStatusEffectFactory : IStatusEffectFactory
    {
        /// <summary>GDD §11: every stun option acts on the next turn.</summary>
        private const int NextTurnOnly = 1;

        public IStatusEffect Create(StatusKind kind, DamageSettings settings)
        {
            Guard.NotNull(settings, nameof(settings));
            switch (settings.StunEffect)
            {
                case StunEffect.NoBodyMove:
                    return new NoBodyMoveStatus(kind, NextTurnOnly);
                case StunEffect.ShorterPlanning:
                    return new ShorterPlanningStatus(kind, NextTurnOnly, settings.StunPlanningDurationFraction);
                case StunEffect.LessInk:
                    return new LessInkStatus(kind, NextTurnOnly, settings.StunInkLengthFraction);
                default:
                    throw new ArgumentOutOfRangeException(nameof(settings), settings.StunEffect, "Unknown stun effect.");
            }
        }
    }
}
