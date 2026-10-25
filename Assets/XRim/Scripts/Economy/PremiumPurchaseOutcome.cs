namespace XRim.Economy
{
    /// <summary>What a real-money purchase of premium currency came to.</summary>
    public enum PremiumPurchaseOutcome
    {
        /// <summary>The store took the money: <see cref="PremiumPurchaseResult.Credited"/> is to be added to the wallet.</summary>
        Completed = 0,

        /// <summary>There is no store in this build yet (Session 22).</summary>
        NotAvailable = 1,

        /// <summary>The player backed out.</summary>
        Cancelled = 2,

        /// <summary>The store or the network refused.</summary>
        Failed = 3,
    }
}
