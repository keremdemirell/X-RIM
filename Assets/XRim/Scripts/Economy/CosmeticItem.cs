using XRim.Core;

namespace XRim.Economy
{
    /// <summary>A cosmetic priced in premium currency, earned currency ("some cosmetics", GDD §16), or both.</summary>
    public sealed class CosmeticItem
    {
        public string Id { get; }
        public CosmeticKind Kind { get; }
        public EarnedAmount? EarnedPrice { get; }
        public PremiumAmount? PremiumPrice { get; }

        /// <summary>What the shop shows.</summary>
        public string DisplayName { get; }

        /// <summary>False until the art or sound for it exists: owning it is recorded, but nothing changes in a match.</summary>
        public bool HasEffectYet { get; }

        public CosmeticItem(string id, CosmeticKind kind, EarnedAmount? earnedPrice, PremiumAmount? premiumPrice, string displayName = null,
            bool hasEffectYet = true)
        {
            Id = Guard.NotNull(id, nameof(id));
            Kind = kind;
            EarnedPrice = earnedPrice;
            PremiumPrice = premiumPrice;
            DisplayName = string.IsNullOrEmpty(displayName) ? id : displayName;
            HasEffectYet = hasEffectYet;
        }
    }
}
