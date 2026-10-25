using XRim.Core.Gdd;

namespace XRim.Economy
{
    /// <summary>
    /// The way real money turns into premium currency (GDD §16: Gears buy cosmetics only and can never be converted into earned currency).
    /// Session 18 only has the stub, <see cref="StorePurchasesNotAvailable"/>; the app-store purchase arrives with Session 22. The only
    /// thing a completed purchase may do is add premium currency to the wallet: it never touches the earned balance or a gameplay unlock.
    /// </summary>
    [GddTbd("§16", "Prices and the base business model", Proposal = "Free-to-play is assumed; real-money purchases arrive with Session 22")]
    public interface IPremiumPurchaseGateway
    {
        /// <summary>Asks the store to sell the player a bundle of premium currency.</summary>
        PremiumPurchaseResult RequestPurchase();
    }
}
