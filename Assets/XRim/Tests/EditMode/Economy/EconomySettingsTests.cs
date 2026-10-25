using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using XRim.Economy;
using XRim.Rules;

namespace XRim.Tests.EditMode.Economy
{
    /// <summary>The starting economy (GDD §16; every value a placeholder the designer set on 2026-10-25) and the shop data built from it.</summary>
    public sealed class EconomySettingsTests
    {
        [Test]
        public void Defaults_AreTheDesignersPlaceholders()
        {
            var settings = new EconomySettings();

            Assert.That(settings.EarnedCurrencyName, Is.EqualTo("Bolts"));
            Assert.That(settings.PremiumCurrencyName, Is.EqualTo("Gears"));
            Assert.That(settings.RewardPerWin, Is.EqualTo(100L));
            Assert.That(settings.RewardPerHit, Is.EqualTo(0L), "Paying per hit stays a switch, off");
            Assert.That(settings.RewardPerParry, Is.EqualTo(0L));
            Assert.That(settings.PerMatchRewardCap, Is.EqualTo(150L));
            Assert.That(settings.FriendMatchRewardMultiplier, Is.EqualTo(1f));
            Assert.That(settings.PremiumEarnableInPlay, Is.False);
            Assert.That(settings.Pays(MatchKind.VersusBot), Is.True);
            Assert.That(settings.Pays(MatchKind.HotSeat), Is.False);
            Assert.That(settings.Pays(MatchKind.Practice), Is.False);
        }

        [Test]
        public void Defaults_HaveNothingToReport()
        {
            var issues = new List<string>();

            new EconomySettings().Validate(issues);

            Assert.That(issues, Is.Empty);
        }

        [Test]
        public void StartingShop_NewPlayerOwnsTheRapierMaceShieldAndOneMove()
        {
            ShopCatalogue catalogue = ShopCatalogue.From(new EconomySettings());

            var starting = catalogue.StartingUnlocks().Select(unlock => unlock.Kind + ":" + unlock.Id).ToList();

            Assert.That(starting, Is.EquivalentTo(new[]
            {
                "Weapon:rapier", "Weapon:mace", "Shield:shield", "SignatureMove:" + SignatureMoveIds.FiveFingerExplodingTouch.Value,
            }));
        }

        [Test]
        public void StartingShop_SwordSpearAndTheOtherMovesAreUnlocks()
        {
            ShopCatalogue catalogue = ShopCatalogue.From(new EconomySettings());

            Assert.That(catalogue.FindUnlock(GameplayUnlockKind.Weapon, WeaponIds.Sword.Value).Price.Value, Is.EqualTo(300L));
            Assert.That(catalogue.FindUnlock(GameplayUnlockKind.Weapon, WeaponIds.Spear.Value).Price.Value, Is.EqualTo(300L));
            Assert.That(catalogue.FindUnlock(GameplayUnlockKind.SignatureMove, SignatureMoveIds.HeadImpactSimulation404.Value).Price.Value,
                Is.EqualTo(400L));
            Assert.That(catalogue.FindUnlock(GameplayUnlockKind.SignatureMove, SignatureMoveIds.WhiplashCalibrationSpin.Value).Price.Value,
                Is.EqualTo(400L));
            Assert.That(catalogue.FindUnlock(GameplayUnlockKind.Weapon, WeaponIds.Sword.Value).StartsOwned, Is.False);
        }

        [Test]
        public void StartingShop_HasFourPlaceholderCosmetics_AndOnlyNeonSparksHasAnEffect()
        {
            ShopCatalogue catalogue = ShopCatalogue.From(new EconomySettings());

            Assert.That(catalogue.Cosmetics.Count, Is.EqualTo(4));
            Assert.That(catalogue.Cosmetics.Where(item => item.HasEffectYet).Select(item => item.Id), Is.EqualTo(new[] { CosmeticIds.NeonSparks }));
            Assert.That(catalogue.FindCosmetic(CosmeticIds.NeonSparks).Kind, Is.EqualTo(CosmeticKind.HitEffect));
            Assert.That(catalogue.FindCosmetic(CosmeticIds.NeonSparks).EarnedPrice.Value.Value, Is.EqualTo(250L));
            Assert.That(catalogue.FindCosmetic(CosmeticIds.NeonSparks).PremiumPrice.Value.Value, Is.EqualTo(50L));
            Assert.That(catalogue.FindCosmetic(CosmeticIds.NightShift).EarnedPrice.HasValue, Is.False, "A premium-only cosmetic has no earned price");
            Assert.That(catalogue.FindCosmetic(CosmeticIds.HazardStripes).PremiumPrice.HasValue, Is.False);
        }

        [Test]
        public void Catalogue_PriceOfZeroMeansNotSold_ForACosmetic()
        {
            var settings = new EconomySettings
            {
                Cosmetics = new List<CosmeticEntry> { new CosmeticEntry("x", "X", CosmeticKind.Skin, 0, 0, false) },
            };

            CosmeticItem item = ShopCatalogue.From(settings).FindCosmetic("x");

            Assert.That(item.EarnedPrice.HasValue, Is.False);
            Assert.That(item.PremiumPrice.HasValue, Is.False);
        }

        [Test]
        public void Catalogue_AnIdListedTwice_KeepsTheFirst()
        {
            var settings = new EconomySettings
            {
                Unlockables = new List<UnlockableEntry>
                {
                    new UnlockableEntry("sword", GameplayUnlockKind.Weapon, 300, false),
                    new UnlockableEntry("sword", GameplayUnlockKind.Weapon, 1, false),
                },
            };

            ShopCatalogue catalogue = ShopCatalogue.From(settings);

            Assert.That(catalogue.Unlocks.Count, Is.EqualTo(1));
            Assert.That(catalogue.Unlocks[0].Price.Value, Is.EqualTo(300L));
        }

        [Test]
        public void Catalogue_ANegativePriceBecomesZero()
        {
            var settings = new EconomySettings
            {
                Unlockables = new List<UnlockableEntry> { new UnlockableEntry("sword", GameplayUnlockKind.Weapon, -5, false) },
            };

            Assert.That(ShopCatalogue.From(settings).Unlocks[0].Price.Value, Is.EqualTo(0L));
        }

        [Test]
        public void Validate_ReportsWhatCannotWork()
        {
            var settings = new EconomySettings
            {
                EarnedCurrencyName = " ",
                RewardPerWin = -1,
                PerMatchRewardCap = -1,
                FriendMatchRewardMultiplier = -1f,
                Unlockables = new List<UnlockableEntry>
                {
                    new UnlockableEntry("sword", GameplayUnlockKind.Weapon, 300, false),
                    new UnlockableEntry("sword", GameplayUnlockKind.Weapon, 300, false),
                    new UnlockableEntry("spear", GameplayUnlockKind.Weapon, 0, false),
                    new UnlockableEntry(string.Empty, GameplayUnlockKind.Weapon, 5, false),
                },
                Cosmetics = new List<CosmeticEntry>
                {
                    new CosmeticEntry("skin", "Skin", CosmeticKind.Skin, 0, 0, false),
                    new CosmeticEntry("skin", "Skin", CosmeticKind.Skin, 5, 0, false),
                },
            };
            var issues = new List<string>();

            settings.Validate(issues);

            Assert.That(issues.Any(issue => issue.Contains("no name")), "a currency without a name");
            Assert.That(issues.Any(issue => issue.Contains("reward is negative")), "a negative reward");
            Assert.That(issues.Any(issue => issue.Contains("cap is negative")), "a negative cap");
            Assert.That(issues.Any(issue => issue.Contains("multiplier is negative")), "a negative multiplier");
            Assert.That(issues.Any(issue => issue.Contains("'sword' is listed twice")), "a repeated unlock");
            Assert.That(issues.Any(issue => issue.Contains("'spear' costs 0")), "a free unlock");
            Assert.That(issues.Any(issue => issue.Contains("has no id")), "an unlock without an id");
            Assert.That(issues.Any(issue => issue.Contains("'skin' is listed twice")), "a repeated cosmetic");
            Assert.That(issues.Any(issue => issue.Contains("'skin' is not sold")), "a cosmetic nobody can buy");
        }

        [Test]
        public void StartingShop_AStartingItemIsFreeAndABoughtOneIsNot()
        {
            foreach (UnlockableEntry entry in new EconomySettings().Unlockables)
            {
                Assert.That(entry.Id, Is.Not.Empty);
                Assert.That(entry.EarnedPrice, Is.GreaterThanOrEqualTo(0L));
                Assert.That(entry.StartsOwned, Is.EqualTo(entry.EarnedPrice == 0L), entry.Id + ": a starting item is free and a bought one is not");
            }
        }
    }
}
