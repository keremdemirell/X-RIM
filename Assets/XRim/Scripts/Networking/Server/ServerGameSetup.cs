using System;
using XRim.Core;
using XRim.Rules;
using XRim.Rules.Settings;
using XRim.Simulation;
using XRim.Simulation.Execution;

namespace XRim.Networking.Server
{
    /// <summary>
    /// What a server plays every match with (Session 19): one settings snapshot, the simulator that resolves turns (one at a time: a turn
    /// is simulated inside the server's frame, so matches can share it), the same snapshot as text for the phones, the rule policies for
    /// each match and where match seeds come from.
    /// </summary>
    public sealed class ServerGameSetup
    {
        public RulesSettings Rules { get; }
        public SimulationSettings Simulation { get; }
        public ITurnSimulator Simulator { get; }

        /// <summary>The settings as text (<see cref="Protocol.ISettingsTextCodec"/>), sent to each player when an opponent is found.</summary>
        public string SettingsText { get; }

        /// <summary>The rule policies of one match; fresh for each, so a policy's state never leaks between matches.</summary>
        public Func<RulePolicies> CreatePolicies { get; }

        /// <summary>Where each match's seed comes from (the sudden-death coin toss).</summary>
        public IRandom Seeds { get; }

        public ServerGameSetup(RulesSettings rules, SimulationSettings simulation, ITurnSimulator simulator, string settingsText, IRandom seeds,
            Func<RulePolicies> createPolicies = null)
        {
            Rules = Guard.NotNull(rules, nameof(rules));
            Simulation = Guard.NotNull(simulation, nameof(simulation));
            Simulator = Guard.NotNull(simulator, nameof(simulator));
            SettingsText = Guard.NotNull(settingsText, nameof(settingsText));
            Seeds = Guard.NotNull(seeds, nameof(seeds));
            CreatePolicies = createPolicies ?? (() => new RulePolicies());
        }
    }
}
