using XRim.Core;

namespace XRim.Economy
{
    /// <summary>
    /// The only way an item is bought (GDD §16). The rules it enforces are in its method signatures:
    /// <list type="bullet">
    /// <item>A <see cref="GameplayUnlock"/> has one <c>Buy</c> and it spends earned currency only. There is no overload that takes a premium
    /// balance or a <see cref="CurrencyKind"/>, so a weapon, shield or signature move cannot be bought with real money, directly or
    /// indirectly (Decided).</item>
    /// <item>A <see cref="CosmeticItem"/> may be bought with either balance, and only the balance it is paid with changes.</item>
    /// <item>Nothing here moves value from one balance to the other, and nothing ever will (Decided).</item>
    /// </list>
    /// Buying premium currency for real money is not here: it is a store purchase (Session 22).
    /// </summary>
    public sealed class Shop
    {
        private readonly Wallet _wallet;
        private readonly Inventory _inventory;

        public Shop(Wallet wallet, Inventory inventory)
        {
            _wallet = Guard.NotNull(wallet, nameof(wallet));
            _inventory = Guard.NotNull(inventory, nameof(inventory));
        }

        public PurchaseResult Buy(GameplayUnlock unlock)
        {
            Guard.NotNull(unlock, nameof(unlock));
            if (_inventory.Owns(unlock)) return PurchaseResult.AlreadyOwned;
            if (!_wallet.TrySpend(unlock.Price)) return PurchaseResult.CannotAfford;

            _inventory.Grant(unlock.Kind, unlock.Id);
            return PurchaseResult.Bought;
        }

        public PurchaseResult Buy(CosmeticItem cosmetic, CurrencyKind payWith)
        {
            Guard.NotNull(cosmetic, nameof(cosmetic));
            if (_inventory.OwnsCosmetic(cosmetic.Id)) return PurchaseResult.AlreadyOwned;

            if (payWith == CurrencyKind.Earned)
            {
                if (!cosmetic.EarnedPrice.HasValue) return PurchaseResult.NotSoldForThisCurrency;
                if (!_wallet.TrySpend(cosmetic.EarnedPrice.Value)) return PurchaseResult.CannotAfford;
            }
            else
            {
                if (!cosmetic.PremiumPrice.HasValue) return PurchaseResult.NotSoldForThisCurrency;
                if (!_wallet.TrySpend(cosmetic.PremiumPrice.Value)) return PurchaseResult.CannotAfford;
            }

            _inventory.GrantCosmetic(cosmetic.Kind, cosmetic.Id);
            return PurchaseResult.Bought;
        }
    }
}
