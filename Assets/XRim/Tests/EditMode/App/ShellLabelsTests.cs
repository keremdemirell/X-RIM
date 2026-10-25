using NUnit.Framework;
using XRim.App;
using XRim.Economy;
using XRim.Presentation.UI;
using XRim.Rules;

namespace XRim.Tests.EditMode.App
{
    /// <summary>The words of the menus (Session 18).</summary>
    public sealed class ShellLabelsTests
    {
        [Test]
        public void EveryMenuOption_HasATitleAndALine()
        {
            var economy = new EconomySettings();
            foreach (MenuOption option in ShellLabels.MainMenu())
            {
                Assert.That(option.Title, Is.Not.Empty);
                Assert.That(option.Detail, Is.Not.Empty);
            }

            foreach (MenuOption option in ShellLabels.PlayMenu(economy))
            {
                Assert.That(option.Title, Is.Not.Empty);
                Assert.That(option.Detail, Is.Not.Empty);
            }

            foreach (MenuOption option in ShellLabels.PracticeMenu())
            {
                Assert.That(option.Title, Is.Not.Empty);
                Assert.That(option.Detail, Is.Not.Empty);
            }
        }

        [Test]
        public void TheIndexes_MatchTheMenusTheyNumber()
        {
            Assert.That(ShellLabels.MainMenu()[ShellLabels.MainPlay].Title, Is.EqualTo("PLAY"));
            Assert.That(ShellLabels.MainMenu()[ShellLabels.MainLoadout].Title, Is.EqualTo("LOADOUT"));
            Assert.That(ShellLabels.MainMenu()[ShellLabels.MainShop].Title, Is.EqualTo("SHOP"));
            Assert.That(ShellLabels.MainMenu()[ShellLabels.MainSettings].Title, Is.EqualTo("SETTINGS"));
            Assert.That(ShellLabels.PlayMenu(new EconomySettings())[ShellLabels.PlayPractice].Title, Is.EqualTo("PRACTICE"));
            Assert.That(ShellLabels.PracticeMenu()[ShellLabels.PracticeWatch].Title, Does.Contain("WATCH"));
        }

        [Test]
        public void VsBot_SaysWhatAWinEarns_UsingTheCurrencyNameFromTheSettings()
        {
            var economy = new EconomySettings { RewardPerWin = 100, EarnedCurrencyName = "Bolts" };

            Assert.That(ShellLabels.VersusBotDetail(economy), Does.Contain("100 Bolts"));

            economy.EarnedCurrencyName = "Scrap";
            Assert.That(ShellLabels.VersusBotDetail(economy), Does.Contain("Scrap"));
        }

        [Test]
        public void VsBot_SaysNothingAboutMoney_WhenAWinPaysNothing()
        {
            Assert.That(ShellLabels.VersusBotDetail(new EconomySettings { RewardPerWin = 0 }), Does.Not.Contain("earn"));
            Assert.That(ShellLabels.VersusBotDetail(new EconomySettings { PaysInVersusBot = false }), Does.Not.Contain("earn"));
        }

        [Test]
        public void ACosmeticsPrice_NamesBothCurrenciesWhenItTakesBoth()
        {
            var economy = new EconomySettings();

            Assert.That(ShellLabels.CosmeticPrice(economy, new CosmeticItem("a", CosmeticKind.Skin, new EarnedAmount(250), new PremiumAmount(50))),
                Is.EqualTo("250 Bolts or 50 Gears"));
            Assert.That(ShellLabels.CosmeticPrice(economy, new CosmeticItem("b", CosmeticKind.Skin, null, new PremiumAmount(50))), Is.EqualTo("50 Gears"));
            Assert.That(ShellLabels.CosmeticPrice(economy, new CosmeticItem("c", CosmeticKind.Skin, new EarnedAmount(250), null)), Is.EqualTo("250 Bolts"));
            Assert.That(ShellLabels.CosmeticPrice(economy, new CosmeticItem("d", CosmeticKind.Skin, null, null)), Is.EqualTo(ShellLabels.NotForSale));
        }

        [Test]
        public void ALockedLine_SaysWhatItCostsInTheShop_OrOnlyLocked()
        {
            var economy = new EconomySettings();

            Assert.That(ShellLabels.LockedLine(economy, new EarnedAmount(300)), Is.EqualTo("LOCKED · 300 Bolts in the Shop"));
            Assert.That(ShellLabels.LockedLine(economy, null), Is.EqualTo("LOCKED"));
        }

        [Test]
        public void EveryKindOfLook_HasAName_AndEveryMoveEffectALine()
        {
            foreach (CosmeticKind kind in System.Enum.GetValues(typeof(CosmeticKind)))
            {
                Assert.That(ShellLabels.CosmeticKindName(kind), Is.Not.Empty.And.Not.EqualTo(kind.ToString().ToUpperInvariant()));
            }

            Assert.That(ShellLabels.EffectLine(XRim.Rules.Signature.SignatureEffectIds.KnockbackBurst), Does.Contain("back"));
            Assert.That(ShellLabels.EffectLine(XRim.Rules.Signature.SignatureEffectIds.StunOnHit), Does.Contain("stun"));
            Assert.That(ShellLabels.EffectLine(XRim.Rules.Signature.SignatureEffectIds.DamageBoost), Does.Contain("damage"));
            Assert.That(ShellLabels.EffectLine(string.Empty), Is.Not.Empty, "a move with no special property still has a line");
        }

        [Test]
        public void TheMovesNote_SaysTheyAreEarnedAndNeverBought()
        {
            Assert.That(ShellLabels.MovesNote(new EconomySettings()), Does.Contain("Bolts").And.Contain("never with real money"));
        }

        [Test]
        public void HandednessName_IsOneOfTheTwoChoices()
        {
            Assert.That(ShellLabels.HandednessName(Handedness.Left), Is.EqualTo(ShellLabels.LeftHanded));
            Assert.That(ShellLabels.HandednessName(Handedness.Right), Is.EqualTo(ShellLabels.RightHanded));
        }
    }
}
