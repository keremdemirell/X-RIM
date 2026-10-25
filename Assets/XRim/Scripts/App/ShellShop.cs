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
    /// Builds the Shop screen (Session 18, GDD §16): the same three tabs as the Loadout collection, with a button to buy.
    /// <list type="bullet">
    /// <item>A weapon, shield or signature move has one button and it costs the earned currency only (Decided): there is no way to
    /// show a premium price for one, because a <see cref="GameplayUnlock"/> has none.</item>
    /// <item>A look has up to two buttons, the earned currency first and the premium currency second, each only if the look is sold for it.</item>
    /// <item>The last row of the Looks tab is the way to get premium currency with real money: a stub until the store arrives (Session 22).</item>
    /// <item>What the player owns reads OWNED; what they cannot afford yet is dimmed with how many more they need.</item>
    /// </list>
    /// </summary>
    public static class ShellShop
    {
        public static ShellCollectionModel Build(RulesSettings rules, ShopCatalogue catalogue, PlayerProfile profile, EconomySettings economy,
            int selectedTab, string status)
        {
            Guard.NotNull(rules, nameof(rules));
            Guard.NotNull(catalogue, nameof(catalogue));
            Guard.NotNull(profile, nameof(profile));
            Guard.NotNull(economy, nameof(economy));

            var tabs = new[]
            {
                new ShellCollectionTab(ShellLabels.WeaponsTab, ShellLabels.WeaponsNote, Weapons(rules, catalogue, profile, economy)),
                new ShellCollectionTab(ShellLabels.MovesTab, ShellLabels.MovesNote(economy), Moves(rules, catalogue, profile, economy)),
                new ShellCollectionTab(ShellLabels.LooksTab, ShellLabels.ShopLooksNote(economy), Looks(catalogue, profile, economy)),
            };
            return new ShellCollectionModel(ShellLabels.ShopTitle, tabs, selectedTab, ShellLabels.Back, ShellLabels.WalletLine(economy, profile.Wallet), status);
        }

        /// <summary>
        /// The weapon, shield or move a row of the Weapons or Moves tab sells, with its name; null when the row is not an item (or the shop
        /// does not sell it).
        /// </summary>
        public static GameplayUnlock UnlockAt(RulesSettings rules, ShopCatalogue catalogue, int tab, int row, out string name)
        {
            name = string.Empty;
            if (row < 0) return null;

            if (tab == ShellLabels.TabWeapons)
            {
                int index = 0;
                foreach (WeaponStats weapon in rules.Weapons)
                {
                    if (!LoadoutOptions.IsPickable(weapon)) continue;
                    if (index++ != row) continue;
                    name = weapon.DisplayName;
                    return catalogue.FindUnlock(UnlockKindOf(weapon), weapon.Id);
                }

                return null;
            }

            if (tab == ShellLabels.TabMoves && row < rules.SignatureMoves.Count)
            {
                SignatureMoveStats move = rules.SignatureMoves[row];
                name = move.DisplayName;
                return catalogue.FindUnlock(GameplayUnlockKind.SignatureMove, move.Id);
            }

            return null;
        }

        /// <summary>The row of the Looks tab that sells premium currency (it follows the looks).</summary>
        public static int GearsRow(ShopCatalogue catalogue) => catalogue.Cosmetics.Count;

        private static GameplayUnlockKind UnlockKindOf(WeaponStats weapon) =>
            weapon.Kind == WeaponKind.Shield ? GameplayUnlockKind.Shield : GameplayUnlockKind.Weapon;

        private static List<ShellCollectionRow> Weapons(RulesSettings rules, ShopCatalogue catalogue, PlayerProfile profile, EconomySettings economy)
        {
            var ownership = new ProfileWeaponOwnership(profile.Inventory);
            var rows = new List<ShellCollectionRow>();
            foreach (WeaponStats weapon in rules.Weapons)
            {
                if (!LoadoutOptions.IsPickable(weapon)) continue;
                rows.Add(UnlockRow(weapon.DisplayName.ToUpperInvariant(), ShellLabels.WeaponShortLine(weapon), catalogue.FindUnlock(UnlockKindOf(weapon), weapon.Id),
                    ownership.Owns(weapon.WeaponId), profile.Wallet, economy));
            }

            return rows;
        }

        private static List<ShellCollectionRow> Moves(RulesSettings rules, ShopCatalogue catalogue, PlayerProfile profile, EconomySettings economy)
        {
            var rows = new List<ShellCollectionRow>();
            foreach (SignatureMoveStats move in rules.SignatureMoves)
            {
                rows.Add(UnlockRow(move.DisplayName.ToUpperInvariant(), ShellLabels.EffectLine(move.SpecialEffectId),
                    catalogue.FindUnlock(GameplayUnlockKind.SignatureMove, move.Id),
                    profile.Inventory.Owns(GameplayUnlockKind.SignatureMove, move.Id), profile.Wallet, economy));
            }

            return rows;
        }

        /// <summary>An owned item says OWNED; one for sale shows its price on the button, dimmed with "need N more" when the player cannot pay yet.</summary>
        private static ShellCollectionRow UnlockRow(string title, string detail, GameplayUnlock unlock, bool owned, Wallet wallet, EconomySettings economy)
        {
            if (owned) return new ShellCollectionRow(title, detail, true, ShellLabels.Owned, false);
            if (unlock == null) return new ShellCollectionRow(title, detail, true, ShellLabels.NotSold, false);

            bool canAfford = wallet.Earned >= unlock.Price;
            string line = canAfford ? detail : detail + " · " + ShellLabels.NeedMore(unlock.Price.Value - wallet.Earned.Value, economy.EarnedCurrencyName);
            return new ShellCollectionRow(title, line, !canAfford, ShellLabels.PriceButton(unlock.Price.Value, economy.EarnedCurrencyName), canAfford);
        }

        private static List<ShellCollectionRow> Looks(ShopCatalogue catalogue, PlayerProfile profile, EconomySettings economy)
        {
            var rows = new List<ShellCollectionRow>();
            foreach (CosmeticItem look in catalogue.Cosmetics)
            {
                string title = look.DisplayName.ToUpperInvariant();
                string detail = ShellLabels.CosmeticKindName(look.Kind) + (look.HasEffectYet ? string.Empty : ShellLabels.ArtLater);
                if (profile.Inventory.OwnsCosmetic(look.Id))
                {
                    rows.Add(new ShellCollectionRow(title, detail, true, ShellLabels.Owned, false));
                    continue;
                }

                string earnedButton = string.Empty;
                string premiumButton = string.Empty;
                bool canPayEarned = false;
                bool canPayPremium = false;
                if (look.EarnedPrice.HasValue)
                {
                    earnedButton = ShellLabels.PriceButton(look.EarnedPrice.Value.Value, economy.EarnedCurrencyName);
                    canPayEarned = profile.Wallet.Earned >= look.EarnedPrice.Value;
                }

                if (look.PremiumPrice.HasValue)
                {
                    premiumButton = ShellLabels.PriceButton(look.PremiumPrice.Value.Value, economy.PremiumCurrencyName);
                    canPayPremium = profile.Wallet.Premium >= look.PremiumPrice.Value;
                }

                rows.Add(new ShellCollectionRow(title, detail, !canPayEarned && !canPayPremium, earnedButton, canPayEarned, premiumButton, canPayPremium));
            }

            rows.Add(new ShellCollectionRow(ShellLabels.GetGearsTitle(economy), ShellLabels.GetGearsDetail, false, ShellLabels.GetGearsButton));
            return rows;
        }
    }
}
