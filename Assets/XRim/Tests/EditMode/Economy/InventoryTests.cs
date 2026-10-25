using System.Linq;
using NUnit.Framework;
using XRim.Economy;

namespace XRim.Tests.EditMode.Economy
{
    public sealed class InventoryTests
    {
        [Test]
        public void NewInventory_OwnsNothing()
        {
            var inventory = new Inventory();

            Assert.That(inventory.Owns(GameplayUnlockKind.Weapon, "rapier"), Is.False);
            Assert.That(inventory.OwnsCosmetic("neon_sparks"), Is.False);
            Assert.That(inventory.OwnedUnlocks(), Is.Empty);
        }

        [Test]
        public void Grant_AddsOnce()
        {
            var inventory = new Inventory();

            Assert.That(inventory.Grant(GameplayUnlockKind.Weapon, "sword"), Is.True);
            Assert.That(inventory.Grant(GameplayUnlockKind.Weapon, "sword"), Is.False);
            Assert.That(inventory.Owns(GameplayUnlockKind.Weapon, "sword"), Is.True);
        }

        [Test]
        public void GrantStarting_GivesTheStartingItemsAndNoMore()
        {
            var inventory = new Inventory();

            inventory.GrantStarting(ShopCatalogue.From(new EconomySettings()));

            Assert.That(inventory.OwnedUnlocks().Count(), Is.EqualTo(4));
            Assert.That(inventory.Owns(GameplayUnlockKind.Weapon, "sword"), Is.False);
            Assert.That(inventory.OwnedCosmetics(), Is.Empty);
        }

        [Test]
        public void Equip_NeedsAnOwnedCosmeticOfThatKind()
        {
            var inventory = new Inventory();
            inventory.GrantCosmetic(CosmeticKind.HitEffect, "neon_sparks");

            Assert.That(inventory.Equip(CosmeticKind.HitEffect, "neon_sparks"), Is.True);
            Assert.That(inventory.EquippedCosmetic(CosmeticKind.HitEffect), Is.EqualTo("neon_sparks"));
            Assert.That(inventory.Equip(CosmeticKind.Skin, "neon_sparks"), Is.False, "Another kind");
            Assert.That(inventory.Equip(CosmeticKind.HitEffect, "not_owned"), Is.False);
            Assert.That(inventory.EquippedCosmetic(CosmeticKind.Skin), Is.Null);
        }

        [Test]
        public void Equip_ReplacesThePreviousOneOfTheSameKind_AndUnequipClears()
        {
            var inventory = new Inventory();
            inventory.GrantCosmetic(CosmeticKind.HitEffect, "a");
            inventory.GrantCosmetic(CosmeticKind.HitEffect, "b");
            inventory.Equip(CosmeticKind.HitEffect, "a");

            inventory.Equip(CosmeticKind.HitEffect, "b");
            Assert.That(inventory.EquippedCosmetic(CosmeticKind.HitEffect), Is.EqualTo("b"));

            inventory.Unequip(CosmeticKind.HitEffect);
            Assert.That(inventory.EquippedCosmetic(CosmeticKind.HitEffect), Is.Null);
        }

        [Test]
        public void OwnedLists_AreInAStableOrder_ForSaving()
        {
            var inventory = new Inventory();
            inventory.Grant(GameplayUnlockKind.SignatureMove, "z");
            inventory.Grant(GameplayUnlockKind.Weapon, "b");
            inventory.Grant(GameplayUnlockKind.Weapon, "a");
            inventory.GrantCosmetic(CosmeticKind.Skin, "q");
            inventory.GrantCosmetic(CosmeticKind.HitEffect, "m");

            Assert.That(inventory.OwnedUnlocks().Select(item => item.Id), Is.EqualTo(new[] { "a", "b", "z" }));
            Assert.That(inventory.OwnedCosmetics().Select(item => item.Id), Is.EqualTo(new[] { "q", "m" }), "Skin is declared before HitEffect");
        }
    }
}
