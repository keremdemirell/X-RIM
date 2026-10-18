using System;
using System.Collections.Generic;
using System.Globalization;
using XRim.Core;
using XRim.Presentation.Moments;
using XRim.Presentation.UI;

namespace XRim.App
{
    /// <summary>The words the match scene shows for each mode and player (placeholder copy until the meta shell, Session 18).</summary>
    public static class MatchLabels
    {
        public const string You = "YOU";
        public const string PickerTitle = "X-RIM";
        public const string PickerNote = "First playable · developer opponents (D25) · tuning edits apply from the next match";
        public const string CoverInstruction = "Take the device. The other player looks away.";

        /// <summary>The modes the picker offers, in order.</summary>
        public static IReadOnlyList<MatchMode> PickerModes() => new[] { MatchMode.HotSeat, MatchMode.HumanVsBot, MatchMode.BotVsBot };

        public static MenuOption Option(MatchMode mode)
        {
            switch (mode)
            {
                case MatchMode.HotSeat: return new MenuOption("HOT-SEAT", "Two players, one device: plan in turn behind a cover screen");
                case MatchMode.HumanVsBot: return new MenuOption("VS BOT", "You on the left against a practice bot (the Bots tuning asset picks which)");
                case MatchMode.BotVsBot: return new MenuOption("BOT VS BOT (WATCH)", "Two practice bots fight it out");
                default: throw new ArgumentOutOfRangeException(nameof(mode), mode, "The picker does not offer this mode.");
            }
        }

        /// <summary>What each fighter is called in this mode: the human against the bot is "YOU".</summary>
        public static PerSide<string> FighterNames(MatchMode mode, Side humanSide) => PerSide<string>.Create(side => FighterName(mode, side, humanSide));

        public static string FighterName(MatchMode mode, Side side, Side humanSide)
        {
            switch (mode)
            {
                case MatchMode.HumanVsBot: return side == humanSide ? You : "BOT";
                case MatchMode.HotSeat: return HotSeatPlayer(side);
                case MatchMode.BotVsBot: return side == Side.Left ? "LEFT BOT" : "RIGHT BOT";
                default: return side.ToString().ToUpperInvariant();
            }
        }

        /// <summary>Player 1 plays the left dummy, player 2 the right one.</summary>
        public static string HotSeatPlayer(Side side) => side == Side.Left ? "PLAYER 1" : "PLAYER 2";

        /// <summary>The cover screen's headline for the player about to plan.</summary>
        public static string CoverPlayer(Side side) =>
            HotSeatPlayer(side) + (side == Side.Left ? " (LEFT)" : " (RIGHT)");

        public const string ReplayTitle = "INSTANT REPLAY";
        public const string ReplayHint = "tap to skip";

        /// <summary>The instant replay's caption: who did what (Session 13).</summary>
        public static string MomentCaption(MomentKind kind, Side? hero, PerSide<string> names)
        {
            string who = hero.HasValue && names != null ? names[hero.Value] + ": " : string.Empty;
            switch (kind)
            {
                case MomentKind.SuddenDeathFinish: return hero.HasValue ? who + "SUDDEN DEATH!" : "DEAD EVEN!";
                case MomentKind.DoubleKnockOut: return "DOUBLE K.O.!";
                case MomentKind.KnockOut: return who + "K.O.!";
                case MomentKind.Sever: return who + "SEVERED!";
                case MomentKind.DodgeCounter: return who + "DODGE AND COUNTER!";
                case MomentKind.CounterHit: return who + "COUNTER!";
                case MomentKind.Trade: return "TRADE!";
                case MomentKind.Requested: return "REPLAY";
                default: return who + "CRUSHED!";
            }
        }

        /// <summary>What a tap on the cover screen does; the timer starts with it.</summary>
        public static string CoverAction(float planningSeconds) =>
            "TAP TO START PLANNING (" + planningSeconds.ToString("0.#", CultureInfo.InvariantCulture) + " s)";

        // ----- the pre-match loadout pick (Session 14; placeholder copy until the meta shell, Session 18) -----

        public const string LoadoutNote = "The first weapon you pick is the one you start with. Both loadouts are shown when the match starts.";
        public const string LoadoutSlotsFull = "All slots are full: tap a picked weapon to put it back first.";
        public const string LoadoutLockIn = "LOCK IN";
        public const string LoadoutShieldNeeded = "A SHIELD IS REQUIRED";
        public const string LoadoutCoverInstruction = "Take the device and pick your weapons. The other player looks away.";
        public const string LoadoutCoverAction = "TAP TO PICK WEAPONS";
        public const string RevealTitle = "LOADOUTS";
        public const string RevealAction = "TAP TO START";

        /// <summary>The picker's headline: in hot-seat it names the player picking.</summary>
        public static string LoadoutTitle(MatchMode mode, Side side) =>
            (mode == MatchMode.HotSeat ? HotSeatPlayer(side) + ": PICK " : "PICK YOUR ") + XRim.Rules.RuleConstants.LoadoutSlotCount + " WEAPONS";

        public static string LoadoutPickMore(int count) => "PICK " + count + " MORE";

        public static string WeaponTitle(XRim.Rules.Settings.WeaponStats weapon) => weapon.DisplayName.ToUpperInvariant();

        /// <summary>Two lines of the numbers that make a weapon what it is, for the picker's cards (placeholders show as they are).</summary>
        public static string WeaponDetail(XRim.Rules.Settings.WeaponStats weapon)
        {
            string first = string.Format(CultureInfo.InvariantCulture, "damage {0:0.#} · speed {1:0} · mass {2:0.#}", weapon.BaseDamage,
                weapon.SpeedUnitsPerSecond, weapon.Mass);
            string second = string.Format(CultureInfo.InvariantCulture, "reach {0:0} · ink {1:0}×{2:0}", weapon.ReachBeyondHandUnits,
                weapon.InkLengthUnits, weapon.InkThicknessUnits);
            if (weapon.Rigidity != null && weapon.Rigidity.Enabled) second += " · stiff";
            return first + "\n" + second;
        }
    }
}
