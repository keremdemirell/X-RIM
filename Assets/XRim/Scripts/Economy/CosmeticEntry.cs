using System;
using XRim.Core.Gdd;

namespace XRim.Economy
{
    /// <summary>
    /// One cosmetic in the shop's data (GDD §16, Decided: none may change hitboxes, silhouette, reach or readability). A price of 0 means
    /// "not sold for that currency", not "free".
    /// </summary>
    [Serializable]
    public sealed class CosmeticEntry
    {
        public string Id = string.Empty;
        public string DisplayName = string.Empty;
        public CosmeticKind Kind = CosmeticKind.Skin;

        [Placeholder("§16 prices are TBD (designer 2026-10-25: placeholders)")]
        public long EarnedPrice = 0;

        [Placeholder("§16 prices are TBD (designer 2026-10-25: placeholders)")]
        public long PremiumPrice = 0;

        /// <summary>
        /// False until the art or sound for it exists (Session 17): owning and equipping it is recorded, but nothing changes in a match.
        /// </summary>
        public bool HasEffectYet = false;

        public CosmeticEntry()
        {
        }

        public CosmeticEntry(string id, string displayName, CosmeticKind kind, long earnedPrice, long premiumPrice, bool hasEffectYet)
        {
            Id = id;
            DisplayName = displayName;
            Kind = kind;
            EarnedPrice = earnedPrice;
            PremiumPrice = premiumPrice;
            HasEffectYet = hasEffectYet;
        }
    }
}
