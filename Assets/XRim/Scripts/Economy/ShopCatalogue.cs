using System;
using System.Collections.Generic;
using XRim.Core;

namespace XRim.Economy
{
    /// <summary>
    /// What the shop sells, built from <see cref="EconomySettings"/> (GDD §16): gameplay unlocks priced in earned currency only, and
    /// cosmetics priced in either currency. The data can name an id twice; the first entry wins, and the settings' validation reports it.
    /// </summary>
    public sealed class ShopCatalogue
    {
        private readonly List<GameplayUnlock> _unlocks = new List<GameplayUnlock>();
        private readonly List<CosmeticItem> _cosmetics = new List<CosmeticItem>();

        public IReadOnlyList<GameplayUnlock> Unlocks => _unlocks;
        public IReadOnlyList<CosmeticItem> Cosmetics => _cosmetics;

        public ShopCatalogue(IEnumerable<GameplayUnlock> unlocks, IEnumerable<CosmeticItem> cosmetics)
        {
            foreach (GameplayUnlock unlock in Guard.NotNull(unlocks, nameof(unlocks)))
            {
                if (FindUnlock(unlock.Kind, unlock.Id) == null) _unlocks.Add(unlock);
            }

            foreach (CosmeticItem cosmetic in Guard.NotNull(cosmetics, nameof(cosmetics)))
            {
                if (FindCosmetic(cosmetic.Id) == null) _cosmetics.Add(cosmetic);
            }
        }

        public static ShopCatalogue From(EconomySettings settings)
        {
            Guard.NotNull(settings, nameof(settings));
            var unlocks = new List<GameplayUnlock>();
            foreach (UnlockableEntry entry in settings.Unlockables)
            {
                if (string.IsNullOrEmpty(entry.Id)) continue;
                unlocks.Add(new GameplayUnlock(entry.Id, entry.Kind, new EarnedAmount(Math.Max(entry.EarnedPrice, 0L)), entry.StartsOwned));
            }

            var cosmetics = new List<CosmeticItem>();
            foreach (CosmeticEntry entry in settings.Cosmetics)
            {
                if (string.IsNullOrEmpty(entry.Id)) continue;
                EarnedAmount? earned = entry.EarnedPrice > 0 ? new EarnedAmount(entry.EarnedPrice) : (EarnedAmount?)null;
                PremiumAmount? premium = entry.PremiumPrice > 0 ? new PremiumAmount(entry.PremiumPrice) : (PremiumAmount?)null;
                cosmetics.Add(new CosmeticItem(entry.Id, entry.Kind, earned, premium, entry.DisplayName, entry.HasEffectYet));
            }

            return new ShopCatalogue(unlocks, cosmetics);
        }

        /// <summary>The unlock of this kind and id; null when the shop has none.</summary>
        public GameplayUnlock FindUnlock(GameplayUnlockKind kind, string id)
        {
            foreach (GameplayUnlock unlock in _unlocks)
            {
                if (unlock.Kind == kind && string.Equals(unlock.Id, id, StringComparison.Ordinal)) return unlock;
            }

            return null;
        }

        /// <summary>The cosmetic with this id; null when the shop has none.</summary>
        public CosmeticItem FindCosmetic(string id)
        {
            foreach (CosmeticItem cosmetic in _cosmetics)
            {
                if (string.Equals(cosmetic.Id, id, StringComparison.Ordinal)) return cosmetic;
            }

            return null;
        }

        /// <summary>What a new player already owns.</summary>
        public IEnumerable<GameplayUnlock> StartingUnlocks()
        {
            foreach (GameplayUnlock unlock in _unlocks)
            {
                if (unlock.StartsOwned) yield return unlock;
            }
        }
    }
}
