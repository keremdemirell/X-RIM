using NUnit.Framework;
using XRim.Economy;

namespace XRim.Tests.EditMode.Economy
{
    public sealed class EconomySanityTests
    {
        [Test]
        public void Wallet_KeepsCurrenciesSeparate()
        {
            var wallet = new Wallet();
            wallet.Add(new PremiumAmount(500));

            Assert.That(wallet.Earned, Is.EqualTo(EarnedAmount.Zero), "Premium currency never becomes earned currency (§16)");
            Assert.That(wallet.TrySpend(new EarnedAmount(1)), Is.False);
        }

        [Test]
        public void Wallet_SpendsOnlyWhatItHas()
        {
            var wallet = new Wallet();
            wallet.Add(new EarnedAmount(100));

            Assert.That(wallet.TrySpend(new EarnedAmount(60)), Is.True);
            Assert.That(wallet.TrySpend(new EarnedAmount(60)), Is.False);
            Assert.That(wallet.Earned.Value, Is.EqualTo(40L));
        }
    }
}
