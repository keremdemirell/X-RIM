namespace XRim.Economy
{
    /// <summary>
    /// A player's two balances. There is deliberately no method that turns premium into earned currency
    /// (GDD §16, Decided): adding one would be a design change, not a code change.
    /// </summary>
    public sealed class Wallet
    {
        public EarnedAmount Earned { get; private set; } = EarnedAmount.Zero;
        public PremiumAmount Premium { get; private set; } = PremiumAmount.Zero;

        public void Add(EarnedAmount amount) => Earned += amount;

        public void Add(PremiumAmount amount) => Premium += amount;

        public bool TrySpend(EarnedAmount price)
        {
            if (Earned < price) return false;
            Earned = new EarnedAmount(Earned.Value - price.Value);
            return true;
        }

        public bool TrySpend(PremiumAmount price)
        {
            if (Premium < price) return false;
            Premium = new PremiumAmount(Premium.Value - price.Value);
            return true;
        }
    }
}
