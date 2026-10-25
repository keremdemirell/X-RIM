namespace XRim.Economy
{
    /// <summary>What happened when the shop was asked to sell something.</summary>
    public enum PurchaseResult
    {
        Bought = 0,

        /// <summary>Already in the inventory: nothing was charged.</summary>
        AlreadyOwned = 1,

        /// <summary>The balance of the currency asked for is too low: nothing was charged.</summary>
        CannotAfford = 2,

        /// <summary>The cosmetic has no price in the currency asked for: nothing was charged.</summary>
        NotSoldForThisCurrency = 3,
    }
}
