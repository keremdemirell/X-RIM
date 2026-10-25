using NUnit.Framework;
using XRim.Economy;

namespace XRim.Tests.EditMode.Economy
{
    /// <summary>GDD §16 (Decided): gameplay unlocks cost earned currency only; cosmetics take either; nothing converts.</summary>
    public sealed class ShopTests
    {
        private static readonly GameplayUnlock Sword = new GameplayUnlock("sword", GameplayUnlockKind.Weapon, new EarnedAmount(300));
        private static readonly GameplayUnlock Move = new GameplayUnlock("move", GameplayUnlockKind.SignatureMove, new EarnedAmount(400));

        private static readonly CosmeticItem BothPrices =
            new CosmeticItem("sparks", CosmeticKind.HitEffect, new EarnedAmount(250), new PremiumAmount(50));

        private static readonly CosmeticItem PremiumOnly = new CosmeticItem("night", CosmeticKind.ArenaTheme, null, new PremiumAmount(50));
        private static readonly CosmeticItem EarnedOnly = new CosmeticItem("stripes", CosmeticKind.Skin, new EarnedAmount(250), null);

        private Wallet _wallet;
        private Inventory _inventory;
        private Shop _shop;

        [SetUp]
        public void SetUp()
        {
            _wallet = new Wallet();
            _inventory = new Inventory();
            _shop = new Shop(_wallet, _inventory);
        }

        [Test]
        public void GameplayUnlock_CannotBeBoughtWithPremiumCurrency()
        {
            _wallet.Add(new PremiumAmount(100000));

            Assert.That(_shop.Buy(Sword), Is.EqualTo(PurchaseResult.CannotAfford));
            Assert.That(_shop.Buy(Move), Is.EqualTo(PurchaseResult.CannotAfford));
            Assert.That(_inventory.Owns(Sword), Is.False);
            Assert.That(_wallet.Premium.Value, Is.EqualTo(100000L), "A refused purchase charges nothing");
        }

        [Test]
        public void GameplayUnlock_IsBoughtWithEarnedCurrency_AndNothingElseChanges()
        {
            _wallet.Add(new EarnedAmount(500));
            _wallet.Add(new PremiumAmount(70));

            Assert.That(_shop.Buy(Sword), Is.EqualTo(PurchaseResult.Bought));

            Assert.That(_inventory.Owns(Sword), Is.True);
            Assert.That(_wallet.Earned.Value, Is.EqualTo(200L));
            Assert.That(_wallet.Premium.Value, Is.EqualTo(70L), "Premium currency is never touched by a gameplay unlock");
        }

        [Test]
        public void GameplayUnlock_BoughtTwice_IsNotChargedTwice()
        {
            _wallet.Add(new EarnedAmount(1000));
            _shop.Buy(Sword);

            Assert.That(_shop.Buy(Sword), Is.EqualTo(PurchaseResult.AlreadyOwned));
            Assert.That(_wallet.Earned.Value, Is.EqualTo(700L));
        }

        [Test]
        public void GameplayUnlock_NotEnoughEarned_ChargesNothing()
        {
            _wallet.Add(new EarnedAmount(299));

            Assert.That(_shop.Buy(Sword), Is.EqualTo(PurchaseResult.CannotAfford));
            Assert.That(_wallet.Earned.Value, Is.EqualTo(299L));
            Assert.That(_inventory.Owns(Sword), Is.False);
        }

        [Test]
        public void GameplayUnlock_SameIdOfAnotherKind_IsAnotherItem()
        {
            _wallet.Add(new EarnedAmount(1000));
            _shop.Buy(Sword);

            Assert.That(_inventory.Owns(GameplayUnlockKind.Shield, "sword"), Is.False);
        }

        [Test]
        public void Cosmetic_CanBeBoughtWithEarnedCurrency()
        {
            _wallet.Add(new EarnedAmount(250));
            _wallet.Add(new PremiumAmount(50));

            Assert.That(_shop.Buy(BothPrices, CurrencyKind.Earned), Is.EqualTo(PurchaseResult.Bought));

            Assert.That(_inventory.OwnsCosmetic("sparks"), Is.True);
            Assert.That(_wallet.Earned.Value, Is.EqualTo(0L));
            Assert.That(_wallet.Premium.Value, Is.EqualTo(50L), "Only the currency paid with is charged");
        }

        [Test]
        public void Cosmetic_CanBeBoughtWithPremiumCurrency()
        {
            _wallet.Add(new EarnedAmount(250));
            _wallet.Add(new PremiumAmount(50));

            Assert.That(_shop.Buy(BothPrices, CurrencyKind.Premium), Is.EqualTo(PurchaseResult.Bought));

            Assert.That(_wallet.Premium.Value, Is.EqualTo(0L));
            Assert.That(_wallet.Earned.Value, Is.EqualTo(250L));
        }

        [Test]
        public void Cosmetic_WithoutAPriceInThatCurrency_IsNotSold()
        {
            _wallet.Add(new EarnedAmount(100000));
            _wallet.Add(new PremiumAmount(100000));

            Assert.That(_shop.Buy(PremiumOnly, CurrencyKind.Earned), Is.EqualTo(PurchaseResult.NotSoldForThisCurrency));
            Assert.That(_shop.Buy(EarnedOnly, CurrencyKind.Premium), Is.EqualTo(PurchaseResult.NotSoldForThisCurrency));
            Assert.That(_wallet.Earned.Value, Is.EqualTo(100000L));
            Assert.That(_wallet.Premium.Value, Is.EqualTo(100000L));
        }

        [Test]
        public void Cosmetic_NotEnoughOfTheChosenCurrency_IsRefusedEvenIfTheOtherIsRich()
        {
            _wallet.Add(new EarnedAmount(10));
            _wallet.Add(new PremiumAmount(100000));

            Assert.That(_shop.Buy(BothPrices, CurrencyKind.Earned), Is.EqualTo(PurchaseResult.CannotAfford),
                "Premium currency cannot make up an earned price: there is no conversion");
            Assert.That(_wallet.Earned.Value, Is.EqualTo(10L));
        }

        [Test]
        public void Cosmetic_BoughtTwice_IsNotChargedTwice()
        {
            _wallet.Add(new PremiumAmount(100));
            _shop.Buy(PremiumOnly, CurrencyKind.Premium);

            Assert.That(_shop.Buy(PremiumOnly, CurrencyKind.Premium), Is.EqualTo(PurchaseResult.AlreadyOwned));
            Assert.That(_wallet.Premium.Value, Is.EqualTo(50L));
        }
    }
}
