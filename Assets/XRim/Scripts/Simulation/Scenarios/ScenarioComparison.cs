using System;
using System.Collections.Generic;
using System.Globalization;
using XRim.Core;
using XRim.Rules;

namespace XRim.Simulation.Scenarios
{
    /// <summary>
    /// Compares a replayed turn with the outcome a scenario recorded. The events must come in the same order and agree on everything that
    /// is not a number (kind, side, part, weapon, outcome); times and amounts may drift within the <see cref="ScenarioTolerance"/>. Each
    /// difference comes back as a sentence, so a failing regression test says what changed; none means the same turn.
    /// </summary>
    public static class ScenarioComparison
    {
        public static List<string> Differences(ScenarioOutcome expected, ScenarioOutcome actual, ScenarioTolerance tolerance)
        {
            Guard.NotNull(expected, nameof(expected));
            Guard.NotNull(actual, nameof(actual));
            Guard.NotNull(tolerance, nameof(tolerance));
            var differences = new List<string>();
            if (expected.EndReason != actual.EndReason)
                differences.Add($"the turn ended {actual.EndReason}, recorded {expected.EndReason}");
            if (!Within(expected.DurationMicroseconds, actual.DurationMicroseconds, tolerance.TimeMicroseconds))
                differences.Add($"the turn ran {Milliseconds(actual.DurationMicroseconds)}, recorded {Milliseconds(expected.DurationMicroseconds)}");

            CompareEvents(expected.Events, actual.Events, tolerance, differences);
            foreach (Side side in new[] { Side.Left, Side.Right })
            {
                CompareFighter(side, expected.Get(side), actual.Get(side), tolerance, differences);
            }

            return differences;
        }

        private static void CompareEvents(List<ScenarioEventRecord> expected, List<ScenarioEventRecord> actual, ScenarioTolerance tolerance,
            List<string> differences)
        {
            int shared = Math.Min(expected.Count, actual.Count);
            for (int i = 0; i < shared; i++)
            {
                ScenarioEventRecord recorded = expected[i];
                ScenarioEventRecord replayed = actual[i];
                if (!SameFacts(recorded, replayed))
                {
                    differences.Add($"event {i + 1} is \"{replayed.Describe()}\", recorded \"{recorded.Describe()}\"");
                    return; // everything after a different event is shifted: one sentence says it
                }

                if (!Within(recorded.TimeMicroseconds, replayed.TimeMicroseconds, tolerance.TimeMicroseconds))
                    differences.Add($"event {i + 1} ({recorded.Describe()}) came at {Milliseconds(replayed.TimeMicroseconds)}");
                if (!Within(recorded.Amount, replayed.Amount, tolerance.Amount) || !Within(recorded.SecondAmount, replayed.SecondAmount, tolerance.Amount))
                    differences.Add($"event {i + 1} is \"{replayed.Describe()}\", recorded \"{recorded.Describe()}\" (amounts differ)");
            }

            for (int i = shared; i < expected.Count; i++)
            {
                differences.Add($"recorded event {i + 1} did not happen: \"{expected[i].Describe()}\"");
            }

            for (int i = shared; i < actual.Count; i++)
            {
                differences.Add($"an extra event {i + 1} happened: \"{actual[i].Describe()}\"");
            }
        }

        private static bool SameFacts(ScenarioEventRecord a, ScenarioEventRecord b) =>
            a.Kind == b.Kind && a.Side == b.Side && a.Part == b.Part && a.Detail == b.Detail && a.Flag == b.Flag &&
            string.Equals(a.Id ?? string.Empty, b.Id ?? string.Empty, StringComparison.Ordinal);

        private static void CompareFighter(Side side, ScenarioFighterOutcome expected, ScenarioFighterOutcome actual, ScenarioTolerance tolerance,
            List<string> differences)
        {
            CultureInfo c = CultureInfo.InvariantCulture;
            if (!Within(expected.Hp, actual.Hp, tolerance.Amount))
                differences.Add(string.Format(c, "{0} ended on {1:0.##} HP, recorded {2:0.##}", side, actual.Hp, expected.Hp));
            if (expected.IsDead != actual.IsDead)
                differences.Add($"{side} {(actual.IsDead ? "died" : "survived")}, recorded {(expected.IsDead ? "dead" : "alive")}");
            if (expected.IsStunned != actual.IsStunned) differences.Add($"{side} stunned: {actual.IsStunned}, recorded {expected.IsStunned}");
            if (expected.IsStaggered != actual.IsStaggered) differences.Add($"{side} staggered: {actual.IsStaggered}, recorded {expected.IsStaggered}");
            if (!string.Equals(expected.CurrentWeapon ?? string.Empty, actual.CurrentWeapon ?? string.Empty, StringComparison.Ordinal))
                differences.Add($"{side} holds {actual.CurrentWeapon}, recorded {expected.CurrentWeapon}");
            float distance = Vec2.Distance(expected.PelvisUnits, actual.PelvisUnits);
            if (distance > tolerance.PositionUnits)
                differences.Add(string.Format(c, "{0}'s pelvis ended {1:0.#} units from where it was recorded", side, distance));

            foreach (BodyPart part in BodyParts.All)
            {
                int index = (int)part;
                float recordedDamage = At(expected.LimbDamage, index);
                float replayedDamage = At(actual.LimbDamage, index);
                if (!Within(recordedDamage, replayedDamage, tolerance.Amount))
                    differences.Add(string.Format(c, "{0}'s {1} took {2:0.##} limb damage, recorded {3:0.##}", side, part, replayedDamage, recordedDamage));
                bool recordedSevered = At(expected.Severed, index);
                bool replayedSevered = At(actual.Severed, index);
                if (recordedSevered != replayedSevered)
                    differences.Add($"{side}'s {part} {(replayedSevered ? "was severed" : "stayed on")}, recorded {(recordedSevered ? "severed" : "on")}");
            }
        }

        private static float At(float[] values, int index) => values != null && index < values.Length ? values[index] : 0f;

        private static bool At(bool[] values, int index) => values != null && index < values.Length && values[index];

        private static bool Within(long a, long b, long tolerance) => Math.Abs(a - b) <= tolerance;

        private static bool Within(float a, float b, float tolerance) => Math.Abs(a - b) <= tolerance;

        private static string Milliseconds(long microseconds) =>
            (microseconds / (double)SimTime.MicrosecondsPerMillisecond).ToString("0.0", CultureInfo.InvariantCulture) + " ms";
    }
}
