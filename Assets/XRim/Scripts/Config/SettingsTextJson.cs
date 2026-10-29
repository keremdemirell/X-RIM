using System;
using UnityEngine;
using XRim.Networking.Protocol;
using XRim.Rules.Settings;
using XRim.Simulation;

namespace XRim.Config
{
    /// <summary>
    /// The settings a server plays with, as JSON text for the phones (Session 19, <see cref="ISettingsTextCodec"/>): Unity's serializer, the
    /// one that snapshots the tuning assets (<see cref="SettingsCopy"/>), so every saved settings field travels and a field added later
    /// needs no protocol change.
    /// </summary>
    public sealed class SettingsTextJson : ISettingsTextCodec
    {
        public string Write(RulesSettings rules, SimulationSettings simulation)
        {
            if (rules == null) throw new ArgumentNullException(nameof(rules));
            if (simulation == null) throw new ArgumentNullException(nameof(simulation));
            return JsonUtility.ToJson(new Snapshot { Rules = rules, Simulation = simulation });
        }

        /// <exception cref="FormatException">The text is empty, not JSON, or holds no settings.</exception>
        public void Read(string text, out RulesSettings rules, out SimulationSettings simulation)
        {
            if (string.IsNullOrWhiteSpace(text)) throw new FormatException("The settings text is empty.");
            Snapshot snapshot;
            try
            {
                snapshot = JsonUtility.FromJson<Snapshot>(text);
            }
            catch (ArgumentException exception)
            {
                throw new FormatException("The settings text is not valid JSON: " + exception.Message, exception);
            }

            if (snapshot == null || snapshot.Rules == null || snapshot.Simulation == null || snapshot.Rules.Weapons.Count == 0)
                throw new FormatException("The settings text holds no rules and simulation settings.");
            rules = snapshot.Rules;
            simulation = snapshot.Simulation;
        }

        /// <summary>The file's shape: both snapshots side by side.</summary>
        [Serializable]
        private sealed class Snapshot
        {
            public RulesSettings Rules = new RulesSettings();
            public SimulationSettings Simulation = new SimulationSettings();
        }
    }
}
