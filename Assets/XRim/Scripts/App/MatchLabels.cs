using System;
using System.Collections.Generic;
using System.Globalization;
using XRim.Core;
using XRim.Networking.Protocol;
using XRim.Presentation.Moments;

namespace XRim.App
{
    /// <summary>The words the match scene shows for each mode and player (the menus outside a match are ShellLabels).</summary>
    public static class MatchLabels
    {
        public const string You = "YOU";
        public const string Opponent = "OPPONENT";

        // Online (Session 19; placeholder copy until Session 22).
        public const string OnlineFindingTitle = "FINDING AN OPPONENT";
        public const string OnlineWaitingPickTitle = "WAITING FOR YOUR OPPONENT";
        public const string OnlineWaitingPickNote = "They are picking their weapons.";
        public const string OnlineCancel = "LEAVE";
        public const string OnlineFailedTitle = "COULD NOT PLAY ONLINE";
        public const string OpponentLeftBeforeStart = "Your opponent left before the match started.";
        public const string OnlineSettingsUnreadable = "The server's settings could not be read.";
        public const string ConnectionLostTitle = "CONNECTION LOST";
        public const string OnlineReconnectingTitle = "RECONNECTING…";
        public const string OnlineReconnectingDetail = "Your connection dropped. The match goes on; you rejoin by yourself.";
        public const string OpponentDisconnectedTitle = "OPPONENT DISCONNECTED";
        public const string OpponentDisconnectedDetail = "The match goes on: they can come back.";
        public const string OpponentGoneTitle = "OPPONENT AWAY";
        public const string OpponentGoneDetail = "Their turns are idle until they return.";
        public const string OpponentReconnectingTitle = "OPPONENT RECONNECTING";

        /// <summary>The finding screen's note: where this device looks for the server.</summary>
        public static string OnlineFindingNote(string host, int port) =>
            string.Format(CultureInfo.InvariantCulture, "Server {0}:{1} · waiting for another player", host, port);

        /// <summary>How long the server still holds the next turn for a dropped opponent.</summary>
        public static string OpponentReconnectingDetail(double secondsLeft) =>
            string.Format(CultureInfo.InvariantCulture, "Waiting for them: {0:0} s", Math.Max(0.0, Math.Ceiling(secondsLeft)));

        /// <summary>Why online play stopped, in words (the server's own detail goes to the log, not the screen).</summary>
        public static string OnlineEndReason(GoodbyeReason reason)
        {
            switch (reason)
            {
                case GoodbyeReason.VersionMismatch: return "This build and the server are different versions. Update both.";
                case GoodbyeReason.ReconnectRefused: return "The match you were in is over.";
                case GoodbyeReason.ServerStopping: return "The server stopped.";
                case GoodbyeReason.ProtocolViolation: return "The server refused this device.";
                case GoodbyeReason.MatchOver: return "The match is over.";
                case GoodbyeReason.TimedOut: return "The server heard nothing from this device for too long. Try again.";
                default: return "The server cannot be reached. Is it running, and is the address right (Tuning tab, Network)?";
            }
        }
        public const string CoverInstruction = "Take the device. The other player looks away.";

        /// <summary>What each fighter is called in this mode: the human against the bot is "YOU".</summary>
        public static PerSide<string> FighterNames(MatchMode mode, Side humanSide) => PerSide<string>.Create(side => FighterName(mode, side, humanSide));

        public static string FighterName(MatchMode mode, Side side, Side humanSide)
        {
            switch (mode)
            {
                case MatchMode.HumanVsBot: return side == humanSide ? You : "BOT";
                case MatchMode.HotSeat: return HotSeatPlayer(side);
                case MatchMode.BotVsBot: return side == Side.Left ? "LEFT BOT" : "RIGHT BOT";
                case MatchMode.Online: return side == humanSide ? You : Opponent;
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

        // ----- the pre-match loadout pick (Session 14, GDD §6) -----

        public const string LoadoutNote = "The first weapon you pick is the one you start with. Both loadouts are shown when the match starts.";
        /// <summary>Shown when the player owns no more weapons than there are slots: they pick them all, and are told where more come from.</summary>
        public const string LoadoutFewWeapons = "These are all the weapons you own. Win against the bot to earn more, then spend it in the Shop.";

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
