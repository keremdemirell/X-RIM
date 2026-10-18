using System;
using XRim.Editor.Balance;
using XRim.Editor.Scenarios;

namespace XRim.Editor.PlayModeJobs
{
    /// <summary>The jobs <see cref="PlayModeJobRunner"/> can run, by kind (a kind is stored while Play mode starts, so it is a plain string).</summary>
    internal static class PlayModeJobKinds
    {
        /// <summary>Writes the seed regression scenarios (XRim → Scenarios → Write Seed Fixtures).</summary>
        public const string SeedFixtures = "seed-fixtures";

        /// <summary>Plays the bot-vs-bot balance batch and writes its report (XRim → Balance → Run Bot Batch…); its arguments are the spec as JSON.</summary>
        public const string BotBatch = "bot-batch";

        public static bool IsKnown(string kind) => kind == SeedFixtures || kind == BotBatch;

        public static IPlayModeJob Create(string kind, string arguments)
        {
            switch (kind)
            {
                case SeedFixtures:
                    return new SeedFixtureJob();
                case BotBatch:
                    return new BotBatchJob(arguments);
                default:
                    throw new ArgumentException($"No Play-mode job of kind '{kind}'.", nameof(kind));
            }
        }
    }
}
