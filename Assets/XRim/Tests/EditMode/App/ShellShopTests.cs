using System.Linq;
using NUnit.Framework;
using XRim.App;
using XRim.Economy;
using XRim.Presentation.UI;
using XRim.Profile;
using XRim.Rules;
using XRim.Rules.Settings;

namespace XRim.Tests.EditMode.App
{
    /// <summary>The Shop screen's content (Session 18, GDD §16): what is for sale, what it costs and in which currency.</summary>
    public sealed class ShellShopTests
    {
        private EconomySettings _economy;
        private RulesSettings _rules;
        private ShopCatalogue _catalogue;
        private PlayerProfile _profile;

        [SetUp]
        public void SetUp()
        {
            _economy = new EconomySettings();
            _rules = GddStartingValues.CreateRulesSettings();
            _catalogue = ShopCatalogue.From(_economy);
            _profile = PlayerProfile.CreateNew(_catalogue);
        }

        private ShellCollectionModel Build(int tab = 0, string status = "") => ShellShop.Build(_rules, _catalogue, _profile, _economy, tab, status);

        private ShellCollectionTab Tab(int index) => Build(index).Tabs[index];

        [Test]
        public void TheHeader_ShowsBothBalances_AndTheStatusIsPassedThrough()
        {
            _profile.Wallet.Add(new EarnedAmount(120));
            _profile.Wallet.Add(new PremiumAmount(7));

            ShellCollectionModel model = Build(0, "BOUGHT: SWORD");

            Assert.That(model.Title, Is.EqualTo("SHOP"));
            Assert.That(model.Header, Is.EqualTo("BOLTS 120   GEARS 7"));
            Assert.That(model.Status, Is.EqualTo("BOUGHT: SWORD"));
        }

        [Test]
        public void OwnedWeapons_SayOwned_AndAreNotForSaleAgain()
        {
            ShellCollectionRow rapier = Tab(ShellLabels.TabWeapons).Rows[0];

            Assert.That(rapier.ButtonText, Is.EqualTo(ShellLabels.Owned));
            Assert.That(rapier.ButtonEnabled, Is.False);
            Assert.That(rapier.SecondButtonText, Is.Empty);
        }

        [Test]
        public void ASwordTheyCannotPayForYet_ShowsThePrice_AndHowManyMoreTheyNeed()
        {
            _profile.Wallet.Add(new EarnedAmount(100));

            ShellCollectionRow sword = Tab(ShellLabels.TabWeapons).Rows[1];

            Assert.That(sword.ButtonText, Is.EqualTo("300 BOLTS"));
            Assert.That(sword.ButtonEnabled, Is.False);
            Assert.That(sword.Locked, Is.True);
            Assert.That(sword.Detail, Does.Contain("need 200 more Bolts"));
            Assert.That(sword.Detail, Does.Contain("damage"), "the numbers are shown so they know what they would buy");
        }

        [Test]
        public void ASwordTheyCanPayFor_HasALiveBuyButton()
        {
            _profile.Wallet.Add(new EarnedAmount(300));

            ShellCollectionRow sword = Tab(ShellLabels.TabWeapons).Rows[1];

            Assert.That(sword.ButtonEnabled, Is.True);
            Assert.That(sword.Locked, Is.False);
            Assert.That(sword.Detail, Does.Not.Contain("need"));
        }

        [Test]
        public void WeaponsAndMoves_HaveNoPremiumButton_NoMatterHowRichThePlayerIs()
        {
            _profile.Wallet.Add(new PremiumAmount(1000000));

            foreach (int index in new[] { ShellLabels.TabWeapons, ShellLabels.TabMoves })
            {
                foreach (ShellCollectionRow row in Tab(index).Rows)
                {
                    Assert.That(row.SecondButtonText, Is.Empty, "gameplay unlocks are earned-only (GDD §16)");
                    Assert.That(row.ButtonText, Does.Not.Contain("GEARS"));
                    if (row.ButtonText != ShellLabels.Owned) Assert.That(row.ButtonEnabled, Is.False, "a fortune in Gears buys no weapon: " + row.Title);
                }
            }
        }

        [Test]
        public void Moves_ShowThePriceOfEachUnownedMove()
        {
            ShellCollectionTab moves = Tab(ShellLabels.TabMoves);

            Assert.That(moves.Rows.Select(row => row.ButtonText), Is.EqualTo(new[] { "OWNED", "400 BOLTS", "400 BOLTS" }));
        }

        [Test]
        public void Looks_ShowBothCurrenciesWhereTheyAreSold()
        {
            _profile.Wallet.Add(new EarnedAmount(250));
            _profile.Wallet.Add(new PremiumAmount(10));

            ShellCollectionTab looks = Tab(ShellLabels.TabLooks);

            Assert.That(looks.Rows.Count, Is.EqualTo(_catalogue.Cosmetics.Count + 1), "the looks, then the way to get Gears");
            ShellCollectionRow neon = looks.Rows[0];
            Assert.That(neon.ButtonText, Is.EqualTo("250 BOLTS"));
            Assert.That(neon.ButtonEnabled, Is.True);
            Assert.That(neon.SecondButtonText, Is.EqualTo("50 GEARS"));
            Assert.That(neon.SecondButtonEnabled, Is.False, "10 Gears is not enough for 50");

            ShellCollectionRow night = looks.Rows[2];
            Assert.That(night.ButtonText, Is.Empty, "Night Shift is sold for Gears only");
            Assert.That(night.SecondButtonText, Is.EqualTo("50 GEARS"));
        }

        [Test]
        public void AnOwnedLook_SaysOwned()
        {
            _profile.Inventory.GrantCosmetic(CosmeticKind.HitEffect, CosmeticIds.NeonSparks);

            ShellCollectionRow neon = Tab(ShellLabels.TabLooks).Rows[0];

            Assert.That(neon.ButtonText, Is.EqualTo(ShellLabels.Owned));
            Assert.That(neon.SecondButtonText, Is.Empty);
        }

        [Test]
        public void TheLastLookRow_IsTheRealMoneyStub()
        {
            ShellCollectionRow gears = Tab(ShellLabels.TabLooks).Rows.Last();

            Assert.That(gears.Title, Is.EqualTo("GET GEARS"));
            Assert.That(gears.ButtonText, Is.EqualTo(ShellLabels.GetGearsButton));
            Assert.That(gears.ButtonEnabled, Is.True);
            Assert.That(gears.Detail, Does.Contain("store"));
            Assert.That(ShellShop.GearsRow(_catalogue), Is.EqualTo(_catalogue.Cosmetics.Count));
        }

        [Test]
        public void UnlockAt_MapsRowsToTheUnlocksTheyShow()
        {
            GameplayUnlock sword = ShellShop.UnlockAt(_rules, _catalogue, ShellLabels.TabWeapons, 1, out string swordName);
            GameplayUnlock shield = ShellShop.UnlockAt(_rules, _catalogue, ShellLabels.TabWeapons, 4, out string shieldName);
            GameplayUnlock move = ShellShop.UnlockAt(_rules, _catalogue, ShellLabels.TabMoves, 2, out string moveName);

            Assert.That(sword.Id, Is.EqualTo("sword"));
            Assert.That(swordName, Is.EqualTo("Sword"));
            Assert.That(shield.Kind, Is.EqualTo(GameplayUnlockKind.Shield), "the shield is its own kind of unlock");
            Assert.That(shieldName, Is.Not.Empty);
            Assert.That(move.Id, Is.EqualTo(SignatureMoveIds.WhiplashCalibrationSpin.Value));
            Assert.That(moveName, Is.EqualTo("Whiplash Calibration Spin"));
        }

        [Test]
        public void UnlockAt_ARowThatDoesNotExist_IsNull()
        {
            Assert.That(ShellShop.UnlockAt(_rules, _catalogue, ShellLabels.TabWeapons, 9, out _), Is.Null);
            Assert.That(ShellShop.UnlockAt(_rules, _catalogue, ShellLabels.TabMoves, -1, out _), Is.Null);
            Assert.That(ShellShop.UnlockAt(_rules, _catalogue, ShellLabels.TabLooks, 0, out _), Is.Null, "a look is not a gameplay unlock");
        }
    }
}
