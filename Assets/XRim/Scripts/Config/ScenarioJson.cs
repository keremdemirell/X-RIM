using System;
using System.Globalization;
using UnityEngine;
using XRim.Simulation.Scenarios;

namespace XRim.Config
{
    /// <summary>
    /// Scenario files as JSON text (Session 16), through Unity's serializer: the same one that snapshots the tuning assets
    /// (<see cref="SettingsCopy"/>), so every saved settings field is in the file. The engine-free <see cref="ScenarioCodec"/> does the
    /// conversion; this only reads and writes text.
    /// </summary>
    public static class ScenarioJson
    {
        /// <summary>ISO 8601 date and time, for people reading the file.</summary>
        private const string SavedAtFormat = "s";

        public static string Write(Scenario scenario, DateTime savedAt) =>
            JsonUtility.ToJson(ScenarioCodec.ToData(scenario, savedAt.ToString(SavedAtFormat, CultureInfo.InvariantCulture)), true);

        /// <exception cref="FormatException">The text is empty, not JSON, or not a scenario.</exception>
        /// <exception cref="NotSupportedException">The file was written by newer code.</exception>
        public static Scenario Read(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new FormatException("The scenario file is empty.");
            ScenarioData data;
            try
            {
                data = JsonUtility.FromJson<ScenarioData>(json);
            }
            catch (ArgumentException exception)
            {
                throw new FormatException("The scenario file is not valid JSON: " + exception.Message, exception);
            }

            return ScenarioCodec.FromData(data);
        }
    }
}
