namespace XRim.Economy
{
    /// <summary>The Session 18 stand-in for the app store: it sells nothing and says why.</summary>
    public sealed class StorePurchasesNotAvailable : IPremiumPurchaseGateway
    {
        public const string Reason = "Buying with real money arrives with the store. Nothing was charged.";

        public PremiumPurchaseResult RequestPurchase() =>
            new PremiumPurchaseResult(PremiumPurchaseOutcome.NotAvailable, PremiumAmount.Zero, Reason);
    }
}
