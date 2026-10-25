using System.Collections.Generic;
using XRim.Core;
using XRim.Economy;
using XRim.Presentation.UI;
using XRim.Profile;
using XRim.Rules;
using XRim.Rules.Match;
using XRim.Rules.Settings;

namespace XRim.App
{
    /// <summary>
    /// Builds the Loadout collection screen (Session 18, designer 2026-10-25, Q5): what the player owns, read-only, in three tabs. Weapons
    /// and shield with their numbers, signature moves with what they do, and looks with a button to put one on. Everything the player does
    /// not own yet is listed dimmed with what it costs in the Shop, so there is always a goal on screen. Only a look can be changed here
    /// (equipped or removed); the three-slot weapon pick stays right before each match (Session 14).
    /// </summary>
    public static class ShellCollection
    {
        public const int TabCount = 3;

        public static ShellCollectionModel Build(RulesSettings rules, ShopCatalogue catalogue, PlayerProfile profile, EconomySettings economy,
            int selectedTab)
        {
            Guard.NotNull(rules, nameof(rules));
            Guard.NotNull(catalogue, nameof(catalogue));
            Guard.NotNull(profile, nameof(profile));
            Guard.NotNull(economy, nameof(economy));

            var tabs = new[]
            {
                new ShellCollectionTab(ShellLabels.WeaponsTab, ShellLabels.WeaponsNote, Weapons(rules, catalogue, profile, economy)),
                new ShellCollectionTab(ShellLabels.MovesTab, ShellLabels.MovesNote(economy), Moves(rules, catalogue, profile, economy)),
                new ShellCollectionTab(ShellLabels.LooksTab, ShellLabels.LooksNote, Looks(catalogue, profile, economy)),
            };
            return new ShellCollectionModel(ShellLabels.CollectionTitle, tabs, selectedTab, ShellLabels.Back);
        }

        /// <summary>The look a row of the Looks tab stands for (the rows follow the shop's order); null for a row that does not exist.</summary>
        public static CosmeticItem LookAt(ShopCatalogue catalogue, int row) =>
            row >= 0 && row < catalogue.Cosmetics.Count ? catalogue.Cosmetics[row] : null;

        private static List<ShellCollectionRow> Weapons(RulesSettings rules, ShopCatalogue catalogue, PlayerProfile profile, EconomySettings economy)
        {
            var ownership = new ProfileWeaponOwnership(profile.Inventory);
            var rows = new List<ShellCollectionRow>();
            foreach (WeaponStats weapon in rules.Weapons)
            {
                if (!LoadoutOptions.IsPickable(weapon)) continue;

                string title = weapon.DisplayName.ToUpperInvariant();
                if (ownership.Owns(weapon.WeaponId))
                {
                    rows.Add(new ShellCollectionRow(title, ShellLabels.WeaponLine(weapon), false));
                    continue;
                }

                GameplayUnlockKind kind = weapon.Kind == WeaponKind.Shield ? GameplayUnlockKind.Shield : GameplayUnlockKind.Weapon;
                GameplayUnlock unlock = catalogue.FindUnlock(kind, weapon.Id);
                rows.Add(new ShellCollectionRow(title, ShellLabels.LockedLine(economy, unlock?.Price), true));
            }

            return rows;
        }

        private static List<ShellCollectionRow> Moves(RulesSettings rules, ShopCatalogue catalogue, PlayerProfile profile, EconomySettings economy)
        {
            var rows = new List<ShellCollectionRow>();
            foreach (SignatureMoveStats move in rules.SignatureMoves)
            {
                string title = move.DisplayName.ToUpperInvariant();
                if (profile.Inventory.Owns(GameplayUnlockKind.SignatureMove, move.Id))
                {
                    rows.Add(new ShellCollectionRow(title, ShellLabels.EffectLine(move.SpecialEffectId), false));
                    continue;
                }

                GameplayUnlock unlock = catalogue.FindUnlock(GameplayUnlockKind.SignatureMove, move.Id);
                rows.Add(new ShellCollectionRow(title, ShellLabels.LockedLine(economy, unlock?.Price), true));
            }

            return rows;
        }

        private static List<ShellCollectionRow> Looks(ShopCatalogue catalogue, PlayerProfile profile, EconomySettings economy)
        {
            var rows = new List<ShellCollectionRow>();
            foreach (CosmeticItem look in catalogue.Cosmetics)
            {
                string kind = ShellLabels.CosmeticKindName(look.Kind);
                string unfinished = look.HasEffectYet ? string.Empty : ShellLabels.ArtLater;
                if (!profile.Inventory.OwnsCosmetic(look.Id))
                {
                    string price = ShellLabels.CosmeticPrice(economy, look);
                    rows.Add(new ShellCollectionRow(look.DisplayName.ToUpperInvariant(), kind + " · " + ShellLabels.LockedWord + " · " + price, true));
                    continue;
                }

                bool equipped = profile.Inventory.EquippedCosmetic(look.Kind) == look.Id;
                rows.Add(new ShellCollectionRow(look.DisplayName.ToUpperInvariant() + (equipped ? "  (ON)" : string.Empty),
                    kind + unfinished, false, equipped ? ShellLabels.Remove : ShellLabels.Equip));
            }

            return rows;
        }
    }
}
