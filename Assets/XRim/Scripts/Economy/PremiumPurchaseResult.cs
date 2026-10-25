namespace XRim.Economy
{
    public readonly struct PremiumPurchaseResult
    {
        public PremiumPurchaseOutcome Outcome { get; }

        /// <summary>Premium currency to add to the wallet; zero unless the purchase <see cref="PremiumPurchaseOutcome.Completed"/>.</summary>
        public PremiumAmount Credited { get; }

        /// <summary>One plain sentence for the player.</summary>
        public string Message { get; }

        public PremiumPurchaseResult(PremiumPurchaseOutcome outcome, PremiumAmount credited, string message)
        {
            Outcome = outcome;
            Credited = credited;
            Message = message ?? string.Empty;
        }
    }
}
