using System.Collections.Generic;
using System.Globalization;
using XRim.Economy;
using XRim.Presentation.UI;
using XRim.Rules;
using XRim.Rules.Settings;
using XRim.Rules.Signature;

namespace XRim.App
{
    /// <summary>
    /// The words of the screens outside a match (Session 18): the handedness question, the menus and Settings. Placeholder copy; the
    /// final wording and the art come later. Currency names come from the economy settings, never from here.
    /// </summary>
    public static class ShellLabels
    {
        public const string GameTitle = "X-RIM";
        public const string MainNote = "Plan in secret. Fight at once.";
        public const string Back = "BACK";

        // ----- the first-launch question (GDD §4, Decided) -----

        public const string HandednessQuestion = "WHICH HAND HOLDS THE WEAPON?";
        public const string HandednessAnswerNote = "Your dummy swings with this hand and the screen is laid out for it. You can change it later in Settings.";
        public const string RightHanded = "RIGHT-HANDED";
        public const string LeftHanded = "LEFT-HANDED";

        // ----- the menus -----

        public const string PlayTitle = "PLAY";
        public const string PlayNote = "Choose who you fight";
        public const string PracticeTitle = "PRACTICE";
        public const string PracticeNote = "Nothing is at stake here: try weapons, or watch";

        public const int MainPlay = 0;
        public const int MainLoadout = 1;
        public const int MainShop = 2;
        public const int MainSettings = 3;

        public const int PlayHotSeat = 0;
        public const int PlayVersusBot = 1;
        public const int PlayPractice = 2;

        public const int PracticeHeuristic = 0;
        public const int PracticeRandom = 1;
        public const int PracticeWatch = 2;

        public static IReadOnlyList<MenuOption> MainMenu() => new[]
        {
            new MenuOption("PLAY", "Hot-seat, against a bot, or practice"),
            new MenuOption("LOADOUT", "Your weapons, signature moves and looks"),
            new MenuOption("SHOP", "Spend what you earn on new weapons, moves and looks"),
            new MenuOption("SETTINGS", "Handedness and volume"),
        };

        public static IReadOnlyList<MenuOption> PlayMenu(EconomySettings economy) => new[]
        {
            new MenuOption("HOT-SEAT", "Two players, one device: plan in turn behind a cover screen"),
            new MenuOption("VS BOT", VersusBotDetail(economy)),
            new MenuOption("PRACTICE", "Pick your bot, or watch two bots fight"),
        };

        public static IReadOnlyList<MenuOption> PracticeMenu() => new[]
        {
            new MenuOption("VS THE HEURISTIC BOT", "A bot that reads the board and goes for hits"),
            new MenuOption("VS THE RANDOM BOT", "A bot that does anything that is allowed"),
            new MenuOption("WATCH TWO BOTS", "Sit back and watch"),
        };

        /// <summary>What a bot match says it offers: the reward when a win pays, nothing about money when it does not.</summary>
        public static string VersusBotDetail(EconomySettings economy)
        {
            if (economy != null && economy.Pays(MatchKind.VersusBot) && economy.RewardPerWin > 0)
            {
                return string.Format(CultureInfo.InvariantCulture, "You against the bot. A win earns {0} {1}", economy.RewardPerWin,
                    economy.EarnedCurrencyName);
            }

            return "You against the bot";
        }

        // ----- the Loadout collection (Session 18): what the player owns -----

        public const string CollectionTitle = "LOADOUT";
        public const string WeaponsTab = "WEAPONS";
        public const string MovesTab = "MOVES";
        public const string LooksTab = "LOOKS";
        public const int TabWeapons = 0;
        public const int TabMoves = 1;
        public const int TabLooks = 2;
        public const string WeaponsNote = "Every unlockable weapon is a sidegrade: different, never simply better";
        public const string LooksNote = "Looks never change hitboxes, reach or how a fight reads";
        public const string Equip = "EQUIP";
        public const string Remove = "REMOVE";
        public const string LockedWord = "LOCKED";
        public const string NotForSale = "not for sale";

        /// <summary>Moves are earned with the earned currency only (GDD §8, §16, Decided).</summary>
        public static string MovesNote(EconomySettings economy) =>
            "Moves are earned with " + economy.EarnedCurrencyName + " only, never with real money";

        /// <summary>One line of the numbers that make a weapon what it is (placeholders show as they are).</summary>
        public static string WeaponLine(WeaponStats weapon) => string.Format(CultureInfo.InvariantCulture,
            "damage {0:0.#} · speed {1:0} · mass {2:0.#} · reach {3:0} · ink {4:0}×{5:0}", weapon.BaseDamage, weapon.SpeedUnitsPerSecond,
            weapon.Mass, weapon.ReachBeyondHandUnits, weapon.InkLengthUnits, weapon.InkThicknessUnits);

        /// <summary>A shorter line of numbers for the Shop, where a buy button and "need N more" share the row (the ink is left out).</summary>
        public static string WeaponShortLine(WeaponStats weapon) => string.Format(CultureInfo.InvariantCulture,
            "damage {0:0.#} · speed {1:0} · mass {2:0.#} · reach {3:0}", weapon.BaseDamage, weapon.SpeedUnitsPerSecond, weapon.Mass,
            weapon.ReachBeyondHandUnits);

        /// <summary>"LOCKED · 300 Bolts in the Shop", or only LOCKED when nothing sells it.</summary>
        public static string LockedLine(EconomySettings economy, EarnedAmount? price) => price.HasValue
            ? string.Format(CultureInfo.InvariantCulture, "{0} · {1} {2} in the Shop", LockedWord, price.Value.Value, economy.EarnedCurrencyName)
            : LockedWord;

        /// <summary>"250 Bolts or 50 Gears", "50 Gears", "250 Bolts" or "not for sale".</summary>
        public static string CosmeticPrice(EconomySettings economy, CosmeticItem item)
        {
            string earned = item.EarnedPrice.HasValue ? item.EarnedPrice.Value.Value.ToString(CultureInfo.InvariantCulture) + " " + economy.EarnedCurrencyName : null;
            string premium = item.PremiumPrice.HasValue ? item.PremiumPrice.Value.Value.ToString(CultureInfo.InvariantCulture) + " " + economy.PremiumCurrencyName : null;
            if (earned != null && premium != null) return earned + " or " + premium;
            return earned ?? premium ?? NotForSale;
        }

        public static string CosmeticKindName(CosmeticKind kind)
        {
            switch (kind)
            {
                case CosmeticKind.Skin: return "Skin";
                case CosmeticKind.DummyModel: return "Dummy model";
                case CosmeticKind.ArenaTheme: return "Arena theme";
                case CosmeticKind.HitEffect: return "Hit effect";
                case CosmeticKind.GruntPack: return "Grunt pack";
                default: return kind.ToString();
            }
        }

        /// <summary>What a signature move's special property does, in a few words.</summary>
        public static string EffectLine(string effectId)
        {
            switch (effectId)
            {
                case SignatureEffectIds.KnockbackBurst: return "Sends the victim flying back";
                case SignatureEffectIds.StunOnHit: return "A landed hit stuns";
                case SignatureEffectIds.DamageBoost: return "Its hits deal more damage";
                default: return "A preset move you tap instead of drawing";
            }
        }

        // ----- the Shop (Session 18, GDD §16) -----

        public const string ShopTitle = "SHOP";
        public const string Owned = "OWNED";
        public const string NotSold = "NOT SOLD";
        public const string ArtLater = " · the look arrives with the art";
        public const string GetGearsDetail = "Premium currency, for looks only. Real-money purchases arrive with the store";
        public const string GetGearsButton = "BUY";

        public static string GetGearsTitle(EconomySettings economy) => "GET " + economy.PremiumCurrencyName.ToUpperInvariant();

        /// <summary>"BOLTS 350   GEARS 0".</summary>
        public static string WalletLine(EconomySettings economy, Wallet wallet) => string.Format(CultureInfo.InvariantCulture, "{0} {1}   {2} {3}",
            economy.EarnedCurrencyName.ToUpperInvariant(), wallet.Earned.Value, economy.PremiumCurrencyName.ToUpperInvariant(), wallet.Premium.Value);

        /// <summary>"300 BOLTS": a buy button.</summary>
        public static string PriceButton(long price, string currencyName) =>
            price.ToString(CultureInfo.InvariantCulture) + " " + currencyName.ToUpperInvariant();

        public static string NeedMore(long missing, string currencyName) =>
            "need " + missing.ToString(CultureInfo.InvariantCulture) + " more " + currencyName;

        public static string ShopLooksNote(EconomySettings economy) =>
            LooksNote + ". " + economy.PremiumCurrencyName + " buy looks only";

        /// <summary>What the last press in the Shop did, in one line.</summary>
        public static string PurchaseStatus(PurchaseResult result, string itemName, CurrencyKind paidWith, EconomySettings economy)
        {
            string currency = paidWith == CurrencyKind.Earned ? economy.EarnedCurrencyName : economy.PremiumCurrencyName;
            switch (result)
            {
                case PurchaseResult.Bought: return "BOUGHT: " + itemName.ToUpperInvariant();
                case PurchaseResult.AlreadyOwned: return "You already own " + itemName;
                case PurchaseResult.CannotAfford: return "Not enough " + currency;
                case PurchaseResult.NotSoldForThisCurrency: return itemName + " is not sold for " + currency;
                default: return string.Empty;
            }
        }

        // ----- Settings -----

        public const string SettingsTitle = "SETTINGS";
        public const string HandednessTitle = "HANDEDNESS";
        public const string HandednessNote = "Your dummy holds its weapon in this hand and the screen is mirrored for it";
        public const string VolumesTitle = "VOLUME";

        public static readonly string[] VolumeNames = { "Master", "Music", "Effects", "Voices" };

        public static string HandednessName(Handedness handedness) => handedness == Handedness.Left ? LeftHanded : RightHanded;
    }
}
