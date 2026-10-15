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
                case MatchMode.HumanVsBot: return new MenuOption("VS RANDOM BOT", "You on the left against the random debug bot");
                case MatchMode.BotVsBot: return new MenuOption("BOT VS BOT (WATCH)", "Two random bots fight it out");
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
    }
}
