using System;
using System.Collections.Generic;
using XRim.Core.Gdd;

namespace XRim.Bots
{
    /// <summary>
    /// The developer and practice opponents (GDD §17, designer 2026-10-17: practice and development only, no game mode): which brain plays
    /// against a human and which two play bot vs bot, and how the heuristic one plays. Wrapped by the Bots tuning asset. Never a rule.
    /// </summary>
    [Serializable]
    public sealed class BotSettings
    {
        [GddTbd("§17", "Bot opponent and practice", Proposal = "Practice and development only (designer 2026-10-17); the heuristic bot by default")]
        public BotBrainKind VersusBrain = BotBrainKind.Heuristic;

        public BotBrainKind WatchLeftBrain = BotBrainKind.Heuristic;
        public BotBrainKind WatchRightBrain = BotBrainKind.Heuristic;

        public HeuristicBotSettings Heuristic = new HeuristicBotSettings();

        /// <summary>A brain of this kind, playing with these settings.</summary>
        public IBotBrain CreateBrain(BotBrainKind kind)
        {
            switch (kind)
            {
                case BotBrainKind.Heuristic:
                    return new HeuristicBotBrain(Heuristic);
                case BotBrainKind.Random:
                    return new RandomBotBrain();
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown bot brain.");
            }
        }

        public void Validate(ICollection<string> issues)
        {
            if (Heuristic == null) issues.Add("Bots: the heuristic bot's settings are missing.");
            else Heuristic.Validate(issues);
        }
    }
}
