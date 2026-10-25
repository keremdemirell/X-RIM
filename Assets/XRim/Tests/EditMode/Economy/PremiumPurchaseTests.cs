using System.Reflection;
using NUnit.Framework;
using XRim.Economy;

namespace XRim.Tests.EditMode.Economy
{
    /// <summary>Real money for premium currency (GDD §16): a stub until the store (Session 22), and only ever premium currency.</summary>
    public sealed class PremiumPurchaseTests
    {
        [Test]
        public void TheStub_SellsNothing_AndSaysWhy()
        {
            PremiumPurchaseResult result = new StorePurchasesNotAvailable().RequestPurchase();

            Assert.That(result.Outcome, Is.EqualTo(PremiumPurchaseOutcome.NotAvailable));
            Assert.That(result.Credited.Value, Is.EqualTo(0L));
            Assert.That(result.Message, Does.Contain("store"));
            Assert.That(result.Message, Does.Contain("Nothing was charged"));
        }

        [Test]
        public void ARealPurchaseResult_CanOnlyCarryPremiumCurrency()
        {
            var result = new PremiumPurchaseResult(PremiumPurchaseOutcome.Completed, new PremiumAmount(500), "ok");

            Assert.That(result.Credited.Value, Is.EqualTo(500L));
            Assert.That(typeof(PremiumPurchaseResult).GetProperty(nameof(PremiumPurchaseResult.Credited)).PropertyType, Is.EqualTo(typeof(PremiumAmount)),
                "real money buys premium currency; nothing in the result can be earned currency (GDD §16)");
        }

        [Test]
        public void TheGatewayInterface_HasNoWayToSellEarnedCurrency()
        {
            foreach (MethodInfo method in typeof(IPremiumPurchaseGateway).GetMethods())
            {
                Assert.That(method.ReturnType, Is.EqualTo(typeof(PremiumPurchaseResult)));
                foreach (ParameterInfo parameter in method.GetParameters())
                {
                    Assert.That(parameter.ParameterType, Is.Not.EqualTo(typeof(EarnedAmount)), "earned currency is never sold for real money (GDD §20, Rejected)");
                }
            }
        }
    }
}
