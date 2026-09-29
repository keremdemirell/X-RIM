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

        public CosmeticItem(string id, CosmeticKind kind, EarnedAmount? earnedPrice, PremiumAmount? premiumPrice)
        {
            Id = Guard.NotNull(id, nameof(id));
            Kind = kind;
            EarnedPrice = earnedPrice;
            PremiumPrice = premiumPrice;
        }
    }
}
