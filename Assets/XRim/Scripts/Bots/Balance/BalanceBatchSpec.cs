using System;
using System.Collections.Generic;

namespace XRim.Bots.Balance
{
    /// <summary>
    /// What a balance batch plays (Session 16): how many matches per run, from which seed, with random or default loadouts, which matchups,
    /// and the limb-durability factors of the sweep. Tool settings, not game values. Every run uses the same seeds, so runs compare like with
    /// like.
    /// </summary>
    [Serializable]
    public sealed class BalanceBatchSpec
    {
        public const int DefaultMatchesPerRun = 100;

        /// <summary>A safety net only: sudden death ends a match within four turns of the turn cap (Session 12).</summary>
        public const int DefaultTurnLimit = 150;

        public int MatchesPerRun = DefaultMatchesPerRun;
        public int FirstSeed = 1;

        /// <summary>Each bot picks a random valid loadout per match (every weapon gets played); off: the default loadout.</summary>
        public bool RandomLoadouts = true;

        public int TurnLimit = DefaultTurnLimit;

        public List<BalanceMatchup> Matchups = new List<BalanceMatchup>
        {
            new BalanceMatchup(BotBrainKind.Heuristic, BotBrainKind.Random),
            new BalanceMatchup(BotBrainKind.Heuristic, BotBrainKind.Heuristic),
        };

        /// <summary>Also plays the sweep matchup with each factor of <see cref="DurabilityFactors"/> (GDD §11: severs in 10–20% of matches).</summary>
        public bool SweepDurability = true;

        /// <summary>Limb durability multipliers for the sweep; 1 always runs as the baseline.</summary>
        public List<float> DurabilityFactors = new List<float> { 0.6f, 0.8f, 1.25f };

        /// <summary>Every run: each matchup as tuned, then the sweep on the first mirror matchup (heuristic vs heuristic), else the last one.</summary>
        public List<BalanceRun> Runs()
        {
            var runs = new List<BalanceRun>();
            if (Matchups == null || Matchups.Count == 0) return runs;
            foreach (BalanceMatchup matchup in Matchups)
            {
                runs.Add(new BalanceRun(matchup, 1f));
            }

            if (!SweepDurability || DurabilityFactors == null) return runs;
            BalanceMatchup swept = Matchups.Find(m => m.IsMirror) ?? Matchups[Matchups.Count - 1];
            foreach (float factor in DurabilityFactors)
            {
                if (factor > 0f && factor != 1f) runs.Add(new BalanceRun(swept, factor));
            }

            return runs;
        }

        public int TotalMatches => Runs().Count * Math.Max(MatchesPerRun, 0);
    }
}
