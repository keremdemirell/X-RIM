using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using XRim.Core;
using XRim.Rules;
using XRim.Rules.Match;
using XRim.Rules.Settings;

namespace XRim.Bots.Balance
{
    /// <summary>
    /// The balance batch's report as Markdown (Session 16; read at PT6): a summary against the GDD's targets (the §11 dismemberment rate;
    /// the §1 match length, which stays TBD, so it is measured and not judged), the heuristic bot against the random one, the durability sweep,
    /// weapons that look strictly stronger or weak (the sidegrade rule, §6 and §16), then every run in detail and every match. It measures and
    /// flags; it decides nothing.
    /// </summary>
    public static class BalanceReport
    {
        /// <summary>The 95% confidence multiplier of a normal approximation (technical).</summary>
        private const double Z95 = 1.96;

        /// <summary>A weapon is only flagged once at least this many sides carried it into a match (technical).</summary>
        private const int MinimumCarriedForFlag = 20;

        private const int SecondsPerMinute = 60;
        private const double Percent = 100.0;

        /// <summary>The turn-count bands of the length table: up to 10, 11–20, 21–30, more.</summary>
        private static readonly int[] TurnBands = { 10, 20, 30 };

        private static readonly CultureInfo C = CultureInfo.InvariantCulture;

        /// <param name="rules">The tuning the batch played with (weapon kinds and the planning time).</param>
        /// <param name="heading">Lines about the run for the top of the report (date, profile, simulation, files).</param>
        /// <param name="partial">The batch stopped before its last match (cancelled or stopped for safety).</param>
        public static string Write(IReadOnlyList<BalanceRunResult> results, BalanceBatchSpec spec, BalanceTargets targets, RulesSettings rules,
            IEnumerable<string> heading, bool partial = false)
        {
            Guard.NotNull(results, nameof(results));
            Guard.NotNull(spec, nameof(spec));
            Guard.NotNull(targets, nameof(targets));
            Guard.NotNull(rules, nameof(rules));
            var text = new StringBuilder();
            text.AppendLine("# X-RIM bot-vs-bot balance batch");
            text.AppendLine();
            foreach (string line in heading ?? Enumerable.Empty<string>())
            {
                text.AppendLine("- " + line);
            }

            int done = results.Sum(r => r.Records.Count);
            if (partial) text.AppendLine($"- **Partial:** stopped after {done} of {spec.TotalMatches} matches; the numbers below cover those.");
            text.AppendLine();

            WriteSummary(text, results, targets, rules);
            foreach (BalanceRunResult result in results)
            {
                WriteRun(text, result, targets, rules);
            }

            WriteHowToRead(text, targets);
            return text.ToString();
        }

        // --- Summary ------------------------------------------------------------------------------

        private static void WriteSummary(StringBuilder text, IReadOnlyList<BalanceRunResult> results, BalanceTargets targets, RulesSettings rules)
        {
            text.AppendLine("## Summary");
            text.AppendLine();
            text.AppendLine("Measured, not decided: every value change is the designer's call at PT6.");
            text.AppendLine();
            foreach (BalanceRunResult result in results.Where(r => r.Run.IsBaseline && !r.Run.Matchup.IsMirror && r.Records.Count > 0))
            {
                BalanceMatchup matchup = result.Run.Matchup;
                int wins = result.Records.Count(r => r.WinningBrain == matchup.First);
                int n = result.Records.Count;
                double share = (double)wins / n;
                double margin = Margin(share, n);
                string verdict = share - margin > 0.5 ? "it beats it" : share + margin < 0.5 ? "it loses to it" : "no clear winner yet";
                text.AppendLine(string.Format(C, "- **{0} against {1}:** {0} won {2} of {3} ({4} ± {5}): {6}.", matchup.First, matchup.Second, wins, n,
                    Pct(share), Pct(margin), verdict));
            }

            foreach (BalanceRunResult result in results.Where(r => r.Run.IsBaseline && r.Records.Count > 0))
            {
                double rate = SeverRate(result);
                text.AppendLine(string.Format(C, "- **Dismemberment ({0}):** {1} of {2} matches had a sever ({3}); the GDD §11 target is {4}–{5}: {6}.",
                    result.Run.Label, result.Records.Count(r => r.Severs > 0), result.Records.Count, Pct(rate), Pct(targets.DismembermentMatchRateMin),
                    Pct(targets.DismembermentMatchRateMax), Verdict(rate, targets)));
            }

            foreach (BalanceRunResult result in results.Where(r => r.Run.IsBaseline && r.Records.Count > 0))
            {
                List<int> turns = result.Records.Select(r => r.Turns).OrderBy(t => t).ToList();
                text.AppendLine(string.Format(C, "- **Match length ({0}):** {1:0.0} turns on average (median {2:0.#}); about {3} with {4:0.#} s of planning " +
                    "a turn (playback alone {5}; the full {6:0.#} s every turn {7}). {8}", result.Run.Label, turns.Average(), Median(turns),
                    Clock(MeanMatchSeconds(result, targets.AssumedPlanningSecondsPerTurn)), targets.AssumedPlanningSecondsPerTurn,
                    Clock(result.Records.Average(r => r.OnScreenSeconds)), rules.Match.PlanningDurationSeconds,
                    Clock(MeanMatchSeconds(result, rules.Match.PlanningDurationSeconds)), LengthVerdict(result, targets)));
            }

            List<BalanceRunResult> sweep = SweepOf(results);
            if (sweep.Count > 1)
            {
                text.AppendLine();
                text.AppendLine($"**Limb durability sweep ({sweep[0].Run.Matchup.Label}), toward the §11 target:**");
                text.AppendLine();
                text.AppendLine("| Factor | Arm / leg durability | Matches with a sever | Mean turns |");
                text.AppendLine("|---|---|---|---|");
                foreach (BalanceRunResult result in sweep)
                {
                    text.AppendLine(string.Format(C, "| ×{0:0.##} | {1:0.#} / {2:0.#} | {3} ({4}) | {5:0.0} |", result.Run.DurabilityFactor, result.ArmDurability,
                        result.LegDurability, result.Records.Count(r => r.Severs > 0), Pct(SeverRate(result)), result.Records.Average(r => r.Turns)));
                }

                text.AppendLine();
                double middle = (targets.DismembermentMatchRateMin + targets.DismembermentMatchRateMax) * 0.5;
                BalanceRunResult closest = sweep.OrderBy(r => Math.Abs(SeverRate(r) - middle)).First();
                bool inside = Verdict(SeverRate(closest), targets) == "within the target";
                text.AppendLine(string.Format(C, "Closest to the middle of the target ({0}): ×{1:0.##} (arm {2:0.#}, leg {3:0.#}), {4}{5}.", Pct(middle),
                    closest.Run.DurabilityFactor, closest.ArmDurability, closest.LegDurability, Pct(SeverRate(closest)),
                    inside ? "" : "; no factor here reaches the 10–20% range, so try factors further out"));
            }

            text.AppendLine();
            foreach (BalanceRunResult result in results.Where(r => r.Run.IsBaseline && r.Records.Count > 0))
            {
                List<string> flags = WeaponFlags(result, rules);
                text.AppendLine(flags.Count == 0
                    ? $"- **Weapons ({result.Run.Label}):** none looks strictly stronger or clearly weak (with at least {MinimumCarriedForFlag} carried each)."
                    : $"- **Weapons ({result.Run.Label}):** " + string.Join(" ", flags));
            }

            text.AppendLine();
        }

        // --- One run ------------------------------------------------------------------------------

        private static void WriteRun(StringBuilder text, BalanceRunResult result, BalanceTargets targets, RulesSettings rules)
        {
            IReadOnlyList<BalanceMatchRecord> records = result.Records;
            text.AppendLine($"## {result.Run.Label}");
            text.AppendLine();
            if (records.Count == 0)
            {
                text.AppendLine("No match finished in this run.");
                text.AppendLine();
                return;
            }

            text.AppendLine(string.Format(C, "Arm durability {0:0.#}, leg durability {1:0.#}. {2} matches, seeds {3}–{4}; the two brains swap sides every match.",
                result.ArmDurability, result.LegDurability, records.Count, records.Min(r => r.Seed), records.Max(r => r.Seed)));
            text.AppendLine();

            text.AppendLine("### Results");
            text.AppendLine();
            text.AppendLine("| Winner | Matches | Share |");
            text.AppendLine("|---|---|---|");
            BalanceMatchup matchup = result.Run.Matchup;
            if (matchup.IsMirror)
            {
                WinRow(text, "Left side", records.Count(r => r.Outcome != null && r.Outcome.Winner == Side.Left), records.Count);
                WinRow(text, "Right side", records.Count(r => r.Outcome != null && r.Outcome.Winner == Side.Right), records.Count);
            }
            else
            {
                WinRow(text, matchup.First.ToString(), records.Count(r => r.WinningBrain == matchup.First), records.Count);
                WinRow(text, matchup.Second.ToString(), records.Count(r => r.WinningBrain == matchup.Second), records.Count);
            }

            text.AppendLine();
            text.AppendLine(string.Format(C, "Ends: KO {0}, forfeit {1}, sudden-death hit {2}, sudden death out of tries: more HP {3}, coin toss {4}; " +
                "unfinished at the turn limit {5}. Won by the loser's own electric wall: {6}.", Ends(records, MatchEndReason.KnockOut),
                Ends(records, MatchEndReason.Forfeit), Ends(records, MatchEndReason.SuddenDeathHit), Ends(records, MatchEndReason.SuddenDeathMoreHp),
                Ends(records, MatchEndReason.SuddenDeathCoinToss), records.Count(r => !r.Finished), records.Count(r => r.EndedByWall)));
            text.AppendLine();

            text.AppendLine("### Dismemberment");
            text.AppendLine();
            double rate = SeverRate(result);
            text.AppendLine(string.Format(C, "Matches with at least one sever: {0} of {1} ({2}); target {3}–{4}: {5}. Severs in all: {6} (arms {7}, legs {8}).",
                records.Count(r => r.Severs > 0), records.Count, Pct(rate), Pct(targets.DismembermentMatchRateMin), Pct(targets.DismembermentMatchRateMax),
                Verdict(rate, targets), records.Sum(r => r.Severs), records.Sum(r => r.ArmSevers), records.Sum(r => r.LegSevers)));
            text.AppendLine();

            text.AppendLine("### Match length");
            text.AppendLine();
            List<int> turns = records.Select(r => r.Turns).OrderBy(t => t).ToList();
            text.AppendLine(string.Format(C, "Turns: mean {0:0.0}, median {1:0.#}, fewest {2}, most {3}. Sudden death reached in {4} of {5}.", turns.Average(),
                Median(turns), turns.First(), turns.Last(), records.Count(r => r.ReachedSuddenDeath), records.Count));
            text.AppendLine();
            text.AppendLine("| Up to 10 turns | 11–20 | 21–30 | More than 30 |");
            text.AppendLine("|---|---|---|---|");
            text.AppendLine(string.Format(C, "| {0} | {1} | {2} | {3} |", turns.Count(t => t <= TurnBands[0]),
                turns.Count(t => t > TurnBands[0] && t <= TurnBands[1]), turns.Count(t => t > TurnBands[1] && t <= TurnBands[2]), turns.Count(t => t > TurnBands[2])));
            text.AppendLine();
            text.AppendLine(string.Format(C, "Time per match: playback on screen {0} on average; with {1:0.#} s of planning a turn {2}; with the full {3:0.#} s {4}.",
                Clock(records.Average(r => r.OnScreenSeconds)), targets.AssumedPlanningSecondsPerTurn,
                Clock(MeanMatchSeconds(result, targets.AssumedPlanningSecondsPerTurn)), rules.Match.PlanningDurationSeconds,
                Clock(MeanMatchSeconds(result, rules.Match.PlanningDurationSeconds))));
            text.AppendLine();

            WriteWeapons(text, result, rules);
            WriteSignatures(text, records);

            text.AppendLine("### Electric wall");
            text.AppendLine();
            text.AppendLine($"Zaps: {records.Sum(r => r.WallZaps)} in all, in {records.Count(r => r.WallZaps > 0)} of {records.Count} matches.");
            text.AppendLine();

            List<double> costs = records.SelectMany(r => r.SimulationMilliseconds).ToList();
            if (costs.Count > 0)
            {
                text.AppendLine("### Simulation cost (this machine)");
                text.AppendLine();
                text.AppendLine(string.Format(C, "Per turn: mean {0:0.0} ms, slowest {1:0.0} ms, over {2} turns.", costs.Average(), costs.Max(), costs.Count));
                text.AppendLine();
            }

            text.AppendLine("### Matches");
            text.AppendLine();
            text.AppendLine("| Seed | Left | Right | Winner | Ended by | Turns | Severs | Zaps |");
            text.AppendLine("|---|---|---|---|---|---|---|---|");
            foreach (BalanceMatchRecord record in records)
            {
                string winner = record.Outcome != null ? $"{record.Outcome.Winner} ({record.Brains[record.Outcome.Winner]})" : "-";
                string reason = record.Outcome != null ? record.Outcome.Reason.ToString() : "unfinished";
                text.AppendLine(string.Format(C, "| {0} | {1}: {2} | {3}: {4} | {5} | {6} | {7} | {8} | {9} |", record.Seed, record.Brains.Left,
                    string.Join(", ", record.Loadouts.Left), record.Brains.Right, string.Join(", ", record.Loadouts.Right), winner, reason, record.Turns,
                    record.Severs, record.WallZaps));
            }

            text.AppendLine();
        }

        private static void WriteWeapons(StringBuilder text, BalanceRunResult result, RulesSettings rules)
        {
            text.AppendLine("### Weapons");
            text.AppendLine();
            text.AppendLine("| Weapon | Carried | Win rate when carried | Turns swung | Hits | Hits per swing | Damage | Damage per swing | Severs | " +
                            "Winning blows | Signature turns (with a hit) |");
            text.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|");
            foreach (WeaponStatsRow row in WeaponRows(result))
            {
                text.AppendLine(string.Format(C, "| {0} | {1} | {2} | {3} | {4} | {5:0.00} | {6:0} | {7:0.0} | {8} | {9} | {10} ({11}) |", row.Weapon,
                    row.Carried, row.Carried > 0 ? Pct(row.WinRate) : "-", row.Swings, row.Hits, row.HitsPerSwing, row.Damage, row.DamagePerSwing, row.Severs,
                    row.Kos, row.SignatureSwings, row.SignatureHitTurns));
            }

            text.AppendLine();
            text.AppendLine("Carried: how many times a side brought it into a match (both sides can carry the same weapon). Win rate when carried: how " +
                            "often that side won.");
            text.AppendLine();
        }

        private static void WriteSignatures(StringBuilder text, IReadOnlyList<BalanceMatchRecord> records)
        {
            var uses = new SortedDictionary<string, int>(StringComparer.Ordinal);
            var hits = new SortedDictionary<string, int>(StringComparer.Ordinal);
            foreach (BalanceMatchRecord record in records)
            {
                foreach (KeyValuePair<SignatureMoveId, int> use in record.SignatureUses) uses[use.Key.Value] = Get(uses, use.Key.Value) + use.Value;
                foreach (KeyValuePair<SignatureMoveId, int> hit in record.SignatureHitTurns) hits[hit.Key.Value] = Get(hits, hit.Key.Value) + hit.Value;
            }

            text.AppendLine("### Signature moves");
            text.AppendLine();
            if (uses.Count == 0)
            {
                text.AppendLine("None played.");
                text.AppendLine();
                return;
            }

            text.AppendLine("| Move | Played | Turns with a hit | Share |");
            text.AppendLine("|---|---|---|---|");
            foreach (KeyValuePair<string, int> use in uses)
            {
                int landed = Get(hits, use.Key);
                text.AppendLine(string.Format(C, "| {0} | {1} | {2} | {3} |", use.Key, use.Value, landed, Pct((double)landed / Math.Max(use.Value, 1))));
            }

            text.AppendLine();
        }

        private static void WriteHowToRead(StringBuilder text, BalanceTargets targets)
        {
            text.AppendLine("## How to read this");
            text.AppendLine();
            text.AppendLine("- The bots are not people: the heuristic bot plays a readable plan from the public board, the random bot scribbles. Use the " +
                            "numbers to compare tunings and weapons with each other, not as a prediction of real matches.");
            text.AppendLine("- ± is the 95% margin of a share (a normal approximation): with 100 matches it is about ±10 points, so small differences " +
                            "are noise.");
            text.AppendLine("- \"Looks strictly stronger\" (the sidegrade rule, GDD §6, §16): a weapon whose win rate when carried is clearly above the " +
                            "others' average (beyond its 95% margin) and whose damage per swing is the highest of the weapons. \"Looks weak\": clearly below.");
            text.AppendLine(string.Format(C, "- Match time adds a person's planning, assumed {0:0.#} s a turn (Balance Targets asset, a placeholder), to the " +
                                             "turns' time on screen with the feel layer.", targets.AssumedPlanningSecondsPerTurn));
        }

        // --- Numbers ------------------------------------------------------------------------------

        private sealed class WeaponStatsRow
        {
            public string Weapon;
            public int Carried;
            public int CarriedWins;
            public int Swings;
            public int Hits;
            public float Damage;
            public int Severs;
            public int Kos;
            public int SignatureSwings;
            public int SignatureHitTurns;
            public double WinRate => Carried > 0 ? (double)CarriedWins / Carried : 0.0;
            public double HitsPerSwing => Swings > 0 ? (double)Hits / Swings : 0.0;
            public double DamagePerSwing => Swings > 0 ? Damage / Swings : 0.0;
        }

        private static List<WeaponStatsRow> WeaponRows(BalanceRunResult result)
        {
            var rows = new SortedDictionary<string, WeaponStatsRow>(StringComparer.Ordinal);
            WeaponStatsRow Row(WeaponId id)
            {
                if (!rows.TryGetValue(id.Value ?? string.Empty, out WeaponStatsRow row))
                {
                    row = new WeaponStatsRow { Weapon = id.Value ?? string.Empty };
                    rows.Add(row.Weapon, row);
                }

                return row;
            }

            foreach (BalanceMatchRecord record in result.Records)
            {
                foreach (Side side in new[] { Side.Left, Side.Right })
                {
                    bool won = record.Outcome != null && record.Outcome.Winner == side;
                    foreach (WeaponId carried in record.Loadouts[side].Distinct())
                    {
                        WeaponStatsRow row = Row(carried);
                        row.Carried++;
                        if (won) row.CarriedWins++;
                    }

                    foreach (KeyValuePair<WeaponId, WeaponTally> used in record.WeaponsOf(side))
                    {
                        WeaponStatsRow row = Row(used.Key);
                        row.Swings += used.Value.Swings;
                        row.Hits += used.Value.Hits;
                        row.Damage += used.Value.Damage;
                        row.Severs += used.Value.Severs;
                        row.Kos += used.Value.Kos;
                        row.SignatureSwings += used.Value.SignatureSwings;
                        row.SignatureHitTurns += used.Value.SignatureHitTurns;
                    }
                }
            }

            return rows.Values.ToList();
        }

        /// <summary>Weapons whose win rate when carried is clearly above (or below) the others' average; "strictly stronger" also needs the top damage.</summary>
        private static List<string> WeaponFlags(BalanceRunResult result, RulesSettings rules)
        {
            var flags = new List<string>();
            List<WeaponStatsRow> rows = WeaponRows(result).Where(r => r.Carried >= MinimumCarriedForFlag).ToList();
            if (rows.Count < 2) return flags;
            foreach (WeaponStatsRow row in rows)
            {
                List<WeaponStatsRow> others = rows.Where(r => r != row).ToList();
                double average = others.Average(r => r.WinRate);
                double margin = Margin(row.WinRate, row.Carried);
                bool isShield = rules.FindWeapon(new WeaponId(row.Weapon))?.Kind == WeaponKind.Shield;
                double topOtherDamage = others.Where(r => rules.FindWeapon(new WeaponId(r.Weapon))?.Kind != WeaponKind.Shield)
                    .Select(r => r.DamagePerSwing).DefaultIfEmpty(0.0).Max();
                if (row.WinRate - average > margin && !isShield && row.DamagePerSwing >= topOtherDamage)
                    flags.Add(string.Format(C, "**{0} looks strictly stronger** (wins {1} when carried against {2} for the others, and the most damage per swing, " +
                        "{3:0.0}): against the sidegrade rule (§6, §16).", row.Weapon, Pct(row.WinRate), Pct(average), row.DamagePerSwing));
                else if (row.WinRate - average > margin)
                    flags.Add(string.Format(C, "{0} wins more often when carried ({1} against {2}) without dealing the most damage: strong, not strictly stronger.",
                        row.Weapon, Pct(row.WinRate), Pct(average)));
                else if (average - row.WinRate > margin)
                    flags.Add(string.Format(C, "{0} looks weak (wins {1} when carried against {2} for the others).", row.Weapon, Pct(row.WinRate), Pct(average)));
            }

            return flags;
        }

        private static List<BalanceRunResult> SweepOf(IReadOnlyList<BalanceRunResult> results)
        {
            BalanceRunResult swept = results.FirstOrDefault(r => !r.Run.IsBaseline);
            if (swept == null) return new List<BalanceRunResult>();
            return results.Where(r => r.Run.Matchup == swept.Run.Matchup && r.Records.Count > 0).OrderBy(r => r.Run.DurabilityFactor).ToList();
        }

        private static double SeverRate(BalanceRunResult result) =>
            result.Records.Count == 0 ? 0.0 : (double)result.Records.Count(r => r.Severs > 0) / result.Records.Count;

        private static string Verdict(double rate, BalanceTargets targets) =>
            rate < targets.DismembermentMatchRateMin ? "below the target" : rate > targets.DismembermentMatchRateMax ? "above the target" : "within the target";

        private static string LengthVerdict(BalanceRunResult result, BalanceTargets targets)
        {
            if (targets.TargetMatchLengthSeconds <= 0f) return "The GDD §1 target match length is not decided yet: measured only.";
            double seconds = MeanMatchSeconds(result, targets.AssumedPlanningSecondsPerTurn);
            return string.Format(C, "Against the {0} target: {1}.", Clock(targets.TargetMatchLengthSeconds),
                seconds > targets.TargetMatchLengthSeconds ? "longer" : "shorter or equal");
        }

        private static double MeanMatchSeconds(BalanceRunResult result, float planningSecondsPerTurn) =>
            result.Records.Average(r => r.OnScreenSeconds + r.Turns * (double)planningSecondsPerTurn);

        private static int Ends(IReadOnlyList<BalanceMatchRecord> records, MatchEndReason reason) =>
            records.Count(r => r.Outcome != null && r.Outcome.Reason == reason);

        private static void WinRow(StringBuilder text, string who, int wins, int n) =>
            text.AppendLine(string.Format(C, "| {0} | {1} | {2} ± {3} |", who, wins, Pct((double)wins / n), Pct(Margin((double)wins / n, n))));

        private static double Margin(double share, int n) => n > 0 ? Z95 * Math.Sqrt(share * (1.0 - share) / n) : 1.0;

        private static double Median(List<int> sorted) =>
            sorted.Count % 2 == 1 ? sorted[sorted.Count / 2] : (sorted[sorted.Count / 2 - 1] + sorted[sorted.Count / 2]) * 0.5;

        private static string Pct(double share) => (share * Percent).ToString("0", C) + "%";

        private static string Clock(double seconds)
        {
            int total = (int)Math.Round(seconds);
            return string.Format(C, "{0}:{1:00}", total / SecondsPerMinute, total % SecondsPerMinute);
        }

        private static int Get(SortedDictionary<string, int> counts, string key) => counts.TryGetValue(key, out int count) ? count : 0;
    }
}
