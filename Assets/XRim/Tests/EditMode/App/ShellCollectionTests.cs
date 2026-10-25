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
    /// <summary>The Loadout collection (Session 18, designer 2026-10-25, Q5): what a player owns, and what is still to win.</summary>
    public sealed class ShellCollectionTests
    {
        private readonly EconomySettings _economy = new EconomySettings();
        private readonly RulesSettings _rules = GddStartingValues.CreateRulesSettings();
        private ShopCatalogue _catalogue;
        private PlayerProfile _profile;

        [SetUp]
        public void SetUp()
        {
            _catalogue = ShopCatalogue.From(_economy);
            _profile = PlayerProfile.CreateNew(_catalogue);
        }

        private ShellCollectionModel Build(int tab = 0) => ShellCollection.Build(_rules, _catalogue, _profile, _economy, tab);

        private ShellCollectionTab Tab(int index) => Build(index).Tabs[index];

        [Test]
        public void ThereAreThreeTabs_WeaponsMovesAndLooks()
        {
            ShellCollectionModel model = Build();

            Assert.That(model.Tabs.Select(tab => tab.Label), Is.EqualTo(new[] { "WEAPONS", "MOVES", "LOOKS" }));
            Assert.That(model.Title, Is.EqualTo(ShellLabels.CollectionTitle));
            Assert.That(model.BackText, Is.EqualTo(ShellLabels.Back));
        }

        [Test]
        public void Weapons_ListEveryPickableWeaponInTheGamesOrder_AndNothingElse()
        {
            ShellCollectionTab weapons = Tab(ShellLabels.TabWeapons);

            Assert.That(weapons.Rows.Select(row => row.Title), Is.EqualTo(new[] { "RAPIER", "SWORD", "MACE", "SPEAR", "SHIELD" }),
                "the severed-limb club and the body blow are never owned or picked");
        }

        [Test]
        public void ANewPlayer_OwnsTheRapierMaceAndShield_AndSeesTheSwordAndSpearAsGoals()
        {
            ShellCollectionTab weapons = Tab(ShellLabels.TabWeapons);

            Assert.That(weapons.Rows.Select(row => row.Locked), Is.EqualTo(new[] { false, true, false, true, false }));
            Assert.That(weapons.Rows[0].Detail, Does.Contain("damage").And.Contain("speed").And.Contain("reach"));
            Assert.That(weapons.Rows[1].Detail, Is.EqualTo("LOCKED · 300 Bolts in the Shop"));
            Assert.That(weapons.Rows[3].Detail, Is.EqualTo("LOCKED · 300 Bolts in the Shop"));
        }

        [Test]
        public void ABoughtWeapon_ShowsItsNumbersInsteadOfAPrice()
        {
            _profile.Inventory.Grant(GameplayUnlockKind.Weapon, "sword");

            ShellCollectionRow sword = Tab(ShellLabels.TabWeapons).Rows[1];

            Assert.That(sword.Locked, Is.False);
            Assert.That(sword.Detail, Does.Contain("damage"));
        }

        [Test]
        public void Moves_ShowTheStarterMoveAndTheTwoToWin()
        {
            ShellCollectionTab moves = Tab(ShellLabels.TabMoves);

            Assert.That(moves.Rows.Count, Is.EqualTo(3));
            Assert.That(moves.Rows.Select(row => row.Locked), Is.EqualTo(new[] { false, true, true }));
            Assert.That(moves.Rows[0].Title, Is.EqualTo("5 FINGER EXPLODING TOUCH"));
            Assert.That(moves.Rows[0].Detail, Is.EqualTo(ShellLabels.EffectLine(XRim.Rules.Signature.SignatureEffectIds.KnockbackBurst)));
            Assert.That(moves.Rows[1].Detail, Is.EqualTo("LOCKED · 400 Bolts in the Shop"));
            Assert.That(moves.Note, Does.Contain("never with real money"));
        }

        [Test]
        public void WeaponsAndMoves_NeverMentionThePremiumCurrency()
        {
            foreach (int index in new[] { ShellLabels.TabWeapons, ShellLabels.TabMoves })
            {
                foreach (ShellCollectionRow row in Tab(index).Rows)
                {
                    Assert.That(row.Detail, Does.Not.Contain(_economy.PremiumCurrencyName), "weapons, shields and moves are earned-only (GDD §16)");
                    Assert.That(row.ButtonText, Is.Empty, "the weapons and moves are looked at here, not changed");
                }
            }
        }

        [Test]
        public void Looks_AreAllLockedForANewPlayer_WithTheirPrices_AndNoButton()
        {
            ShellCollectionTab looks = Tab(ShellLabels.TabLooks);

            Assert.That(looks.Rows.Count, Is.EqualTo(4));
            foreach (ShellCollectionRow row in looks.Rows)
            {
                Assert.That(row.Locked, Is.True);
                Assert.That(row.ButtonText, Is.Empty);
            }

            Assert.That(looks.Rows[0].Title, Is.EqualTo("NEON SPARKS"));
            Assert.That(looks.Rows[0].Detail, Is.EqualTo("Hit effect · LOCKED · 250 Bolts or 50 Gears"));
            Assert.That(looks.Rows[2].Detail, Is.EqualTo("Arena theme · LOCKED · 50 Gears"), "a premium-only look shows only the Gears price");
            Assert.That(looks.Note, Does.Contain("hitboxes"));
        }

        [Test]
        public void AnOwnedLook_HasAnEquipButton_AndRemoveOnceItIsOn()
        {
            _profile.Inventory.GrantCosmetic(CosmeticKind.HitEffect, CosmeticIds.NeonSparks);
            Assert.That(Tab(ShellLabels.TabLooks).Rows[0].ButtonText, Is.EqualTo(ShellLabels.Equip));
            Assert.That(Tab(ShellLabels.TabLooks).Rows[0].Locked, Is.False);

            _profile.Inventory.Equip(CosmeticKind.HitEffect, CosmeticIds.NeonSparks);

            ShellCollectionRow on = Tab(ShellLabels.TabLooks).Rows[0];
            Assert.That(on.ButtonText, Is.EqualTo(ShellLabels.Remove));
            Assert.That(on.Title, Does.Contain("(ON)"));
        }

        [Test]
        public void ALookWithoutItsArtYet_SaysSo()
        {
            _profile.Inventory.GrantCosmetic(CosmeticKind.Skin, CosmeticIds.HazardStripes);
            _profile.Inventory.GrantCosmetic(CosmeticKind.HitEffect, CosmeticIds.NeonSparks);

            ShellCollectionTab looks = Tab(ShellLabels.TabLooks);

            Assert.That(looks.Rows[1].Detail, Does.Contain("arrives with the art"));
            Assert.That(looks.Rows[0].Detail, Does.Not.Contain("arrives with the art"), "Neon Sparks really changes a match");
        }

        [Test]
        public void LookAt_FollowsTheShopsOrder()
        {
            Assert.That(ShellCollection.LookAt(_catalogue, 0).Id, Is.EqualTo(CosmeticIds.NeonSparks));
            Assert.That(ShellCollection.LookAt(_catalogue, 3).Id, Is.EqualTo(CosmeticIds.DrillSergeant));
            Assert.That(ShellCollection.LookAt(_catalogue, 4), Is.Null);
            Assert.That(ShellCollection.LookAt(_catalogue, -1), Is.Null);
        }

        [Test]
        public void TheCurrencyNames_ComeFromTheSettings()
        {
            _economy.EarnedCurrencyName = "Scrap";

            Assert.That(Tab(ShellLabels.TabWeapons).Rows[1].Detail, Is.EqualTo("LOCKED · 300 Scrap in the Shop"));
        }
    }
}
