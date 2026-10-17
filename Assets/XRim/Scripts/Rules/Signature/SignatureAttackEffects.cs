using System;
using System.Collections.Generic;
using XRim.Core;
using XRim.Rules.Settings;

namespace XRim.Rules.Signature
{
    /// <summary>
    /// What a side's signature move does to the hits its own swing lands this turn (GDD §8): its special property from the effect
    /// catalogue, read once with the settings' numbers when the turn begins. <see cref="None"/> for a turn without a move. The hit rules
    /// carry it on the side's attack, so a drawn path, the opponent's hits and every other turn are never touched.
    /// </summary>
    public sealed class SignatureAttackEffects
    {
        public static SignatureAttackEffects None { get; } = new SignatureAttackEffects(default, 1f, 1f, false);

        /// <summary>The move; empty for <see cref="None"/>.</summary>
        public SignatureMoveId Move { get; }

        public float DamageMultiplier { get; }
        public float KnockbackMultiplier { get; }
        public bool StunsOnHit { get; }

        public SignatureAttackEffects(SignatureMoveId move, float damageMultiplier, float knockbackMultiplier, bool stunsOnHit)
        {
            Move = move;
            DamageMultiplier = Math.Max(0f, damageMultiplier);
            KnockbackMultiplier = Math.Max(0f, knockbackMultiplier);
            StunsOnHit = stunsOnHit;
        }

        /// <summary>
        /// The effects of the move a plan holds: no move, or a move the catalogue does not know, is <see cref="None"/>; a move whose effect
        /// id names nothing in <paramref name="catalogue"/> has no special property (it still plays its pattern).
        /// </summary>
        public static SignatureAttackEffects For(SignatureMoveId move, RulesSettings settings, IReadOnlyList<ISignatureEffect> catalogue)
        {
            Guard.NotNull(settings, nameof(settings));
            SignatureMoveStats stats = settings.FindSignatureMove(move);
            if (stats == null) return None;

            ISignatureEffect effect = Find(catalogue, stats.SpecialEffectId);
            if (effect == null) return new SignatureAttackEffects(move, 1f, 1f, false);

            SignatureSettings numbers = settings.Signature;
            return new SignatureAttackEffects(move, effect.DamageMultiplier(numbers), effect.KnockbackMultiplier(numbers), effect.StunsOnHit(numbers));
        }

        private static ISignatureEffect Find(IReadOnlyList<ISignatureEffect> catalogue, string id)
        {
            if (catalogue == null || string.IsNullOrEmpty(id)) return null;
            foreach (ISignatureEffect effect in catalogue)
            {
                if (effect != null && string.Equals(effect.Id, id, StringComparison.Ordinal)) return effect;
            }

            return null;
        }
    }
}
