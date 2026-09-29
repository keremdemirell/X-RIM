using System.Collections.Generic;
using UnityEngine;
using XRim.Config;
using XRim.Rules.Settings;
using XRim.Simulation;
using XRim.Simulation.Unity2D;

namespace XRim.App
{
    /// <summary>
    /// Composition root of a match scene. It builds the settings snapshot, physics world, authority, plan sources and
    /// presentation, and wires them with constructor injection. One per scene; no singletons.
    /// For now it only builds what exists and reports that the match loop is not implemented yet.
    /// </summary>
    public sealed class MatchBootstrap : MonoBehaviour
    {
        [Tooltip("Root of all tuning. Create one with XRim/Setup/Create Default Tuning Assets.")]
        [SerializeField] private TuningProfile _tuning;

        [SerializeField] private MatchMode _mode = MatchMode.HumanVsBot;

        private Unity2DPhysicsWorld _world;

        public TuningProfile Tuning => _tuning;
        public MatchMode Mode => _mode;

        /// <summary>The snapshot the current match uses. Live tuning edits apply to the next snapshot.</summary>
        public RulesSettings RulesSettings { get; private set; }

        public SimulationSettings SimulationSettings { get; private set; }

        private void Start()
        {
            if (_tuning == null)
            {
                Debug.LogError("[XRim] MatchBootstrap has no TuningProfile. Run XRim/Setup/Create Default Tuning Assets, then assign it.", this);
                return;
            }

            // Gameplay physics runs in its own manually stepped scene; cosmetic physics is stepped by playback,
            // so slow motion slows debris too.
            Physics2D.simulationMode = SimulationMode2D.Script;

            var issues = new List<string>();
            RulesSettings = _tuning.BuildRulesSettings(issues);
            SimulationSettings = _tuning.BuildSimulationSettings(issues);
            RulesSettings.Validate(issues);
            foreach (string issue in issues)
            {
                Debug.LogWarning($"[XRim] {issue}", _tuning);
            }

            _world = new Unity2DPhysicsWorld(_tuning.ArenaSpace);
            Debug.Log($"[XRim] Architecture skeleton ready: mode {_mode}, {RulesSettings.Weapons.Count} weapons, " +
                      $"simulation at {SimulationSettings.StepRateHz} Hz. The match loop is not implemented yet.", this);
        }

        private void OnDestroy()
        {
            _world?.Dispose();
            _world = null;
        }

        /// <summary>Serialized field names, for Editor tools that assign references through SerializedObject.</summary>
        public static class FieldNames
        {
            public const string Tuning = nameof(_tuning);
            public const string Mode = nameof(_mode);
        }
    }
}
