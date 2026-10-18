using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using XRim.Config;
using XRim.DebugTools;
using XRim.Editor.PlayModeJobs;
using XRim.Simulation.Execution;
using XRim.Simulation.Recording;
using XRim.Simulation.Scenarios;
using XRim.Simulation.Unity2D;

namespace XRim.Editor.Scenarios
{
    /// <summary>
    /// XRim → Scenarios → Write Seed Fixtures (Session 16): resolves each <see cref="SeedScenarios"/> recipe on real physics, exactly as its
    /// file will hold it (once through the JSON text), checks that the turn shows its moment, and writes the ones that do, with that outcome
    /// recorded, into the regression tests' folder. A recipe whose turn misses is refused with the reason, and an older file of it is kept.
    /// Cancelled or stopped for safety: nothing is written.
    /// </summary>
    internal sealed class SeedFixtureJob : IPlayModeJob
    {
        private readonly IReadOnlyList<SeedScenario> _recipes = SeedScenarios.All();
        private readonly List<KeyValuePair<SeedScenario, Scenario>> _passed = new List<KeyValuePair<SeedScenario, Scenario>>();
        private readonly List<string> _refused = new List<string>();
        private readonly ArenaSpace _space = new ArenaSpace(ArenaSpaceConfig.DefaultWorldUnitsPerArenaUnit);
        private int _next;

        public string Title => "Write Seed Fixtures";

        public string Status => _next < _recipes.Count ? "Resolving: " + _recipes[_next].Title : "Writing the files";

        public float Progress => _recipes.Count == 0 ? 1f : (float)_next / _recipes.Count;

        public bool Step()
        {
            if (_next >= _recipes.Count) return false;
            SeedScenario recipe = _recipes[_next++];

            TurnInput input = recipe.Build(turn => HeadlessPhysics.SimulateOnce(turn, _space));
            var scenario = new Scenario(recipe.Title, recipe.Note, input, _space.WorldUnitsPerArenaUnit);
            Scenario saved = ScenarioJson.Read(ScenarioJson.Write(scenario, DateTime.Now));
            TurnResult result = HeadlessPhysics.SimulateOnce(saved.Input, _space);
            string problem = recipe.Check(saved.Input, result);
            if (problem != null)
            {
                _refused.Add($"{recipe.Title}: {problem}");
                Debug.LogWarning($"[XRim] Seed fixture '{recipe.FileName}' not written: {problem}");
            }
            else
            {
                _passed.Add(new KeyValuePair<SeedScenario, Scenario>(recipe, saved.WithExpected(ScenarioOutcome.Of(result))));
            }

            return _next < _recipes.Count;
        }

        public string Finish(bool cancelled)
        {
            if (cancelled) return "Nothing was written.";

            string folder = ScenarioStore.RegressionFolder();
            Directory.CreateDirectory(folder);
            var message = new StringBuilder();
            message.AppendLine($"Wrote {_passed.Count} of {_recipes.Count} seed fixtures to {ScenarioStore.RegressionFolderInProject}/:");
            foreach (KeyValuePair<SeedScenario, Scenario> passed in _passed)
            {
                string path = Path.Combine(folder, passed.Key.FileName + ScenarioStore.FileExtension);
                File.WriteAllText(path, ScenarioJson.Write(passed.Value, DateTime.Now), new UTF8Encoding(false));
                message.AppendLine("• " + passed.Key.Title);
            }

            if (_refused.Count > 0)
            {
                message.AppendLine();
                message.AppendLine("Not written, because the turn did not show its moment (an older file of it, if any, was kept):");
                foreach (string refused in _refused)
                {
                    message.AppendLine("• " + refused);
                }
            }

            message.AppendLine();
            message.Append("Next: Test Runner → PlayMode → ScenarioRegressionPlayModeTests. Commit the new files with the rest.");
            return message.ToString();
        }
    }
}
