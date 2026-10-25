using System;
using XRim.Bots;
using XRim.Core;
using XRim.Economy;

namespace XRim.App
{
    /// <summary>
    /// A match as the menu asks for it (Session 18): who plays (<see cref="Mode"/>), what it counts as for earned currency
    /// (<see cref="Kind"/>, GDD §16: only a win against the bot pays) and, for practice, which bot. Nothing here is a rule; the rules only
    /// see the fighters and the loadouts.
    /// </summary>
    public readonly struct MatchRequest : IEquatable<MatchRequest>
    {
        public MatchMode Mode { get; }
        public MatchKind Kind { get; }

        /// <summary>The brain every bot in this match plays with; null: the Bots asset's choice.</summary>
        public BotBrainKind? Brain { get; }

        public MatchRequest(MatchMode mode, MatchKind kind, BotBrainKind? brain)
        {
            Mode = mode;
            Kind = kind;
            Brain = brain;
        }

        /// <summary>Two people share this device, planning in turn behind a cover screen.</summary>
        public static MatchRequest HotSeat() => new MatchRequest(MatchMode.HotSeat, MatchKind.HotSeat, null);

        /// <summary>The player against the bot the Bots asset names: the one match that pays by default.</summary>
        public static MatchRequest VersusBot() => new MatchRequest(MatchMode.HumanVsBot, MatchKind.VersusBot, null);

        /// <summary>The player against a bot of their choosing: no stakes, nothing is paid by default.</summary>
        public static MatchRequest Practice(BotBrainKind brain) => new MatchRequest(MatchMode.HumanVsBot, MatchKind.Practice, brain);

        /// <summary>Two bots fight and the player watches.</summary>
        public static MatchRequest WatchBots() => new MatchRequest(MatchMode.BotVsBot, MatchKind.Practice, null);

        /// <summary>The usual request for a mode (tools and tests that only know the mode).</summary>
        public static MatchRequest For(MatchMode mode)
        {
            switch (mode)
            {
                case MatchMode.HotSeat: return HotSeat();
                case MatchMode.HumanVsBot: return VersusBot();
                case MatchMode.BotVsBot: return WatchBots();
                case MatchMode.Sandbox: return new MatchRequest(MatchMode.Sandbox, MatchKind.Practice, null);
                default: throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown match mode.");
            }
        }

        /// <summary>True when a person on this device plans that side.</summary>
        /// <param name="humanSide">The side the person plays against a bot.</param>
        public bool PlaysHumanOn(Side side, Side humanSide)
        {
            switch (Mode)
            {
                case MatchMode.HotSeat: return true;
                case MatchMode.HumanVsBot: return side == humanSide;
                default: return false;
            }
        }

        public bool Equals(MatchRequest other) => Mode == other.Mode && Kind == other.Kind && Brain == other.Brain;

        public override bool Equals(object obj) => obj is MatchRequest other && Equals(other);

        public override int GetHashCode() => ((int)Mode * 31 + (int)Kind) * 31 + (Brain.HasValue ? (int)Brain.Value + 1 : 0);

        public override string ToString() => $"{Mode} ({Kind}{(Brain.HasValue ? ", " + Brain.Value + " bot" : string.Empty)})";

        /// <summary>The bot settings this match plays with: the given ones, or a copy with the chosen brain everywhere.</summary>
        public BotSettings ApplyTo(BotSettings bots)
        {
            if (!Brain.HasValue || bots == null) return bots;
            return new BotSettings
            {
                VersusBrain = Brain.Value,
                WatchLeftBrain = Brain.Value,
                WatchRightBrain = Brain.Value,
                Heuristic = bots.Heuristic,
            };
        }
    }
}
