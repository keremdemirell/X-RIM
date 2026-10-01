using System.Collections.Generic;
using UnityEngine;
using XRim.Economy;
using XRim.Rules.Settings;
using XRim.Simulation;

namespace XRim.Config
{
    /// <summary>
    /// The root of all tuning. Swap profiles to A/B test feel ("baseline" vs "heavy mace"). A match builds its
    /// settings snapshot from the profile at start, so live edits apply from the next match or re-simulation.
    /// Create the default one with the menu XRim/Setup/Create Default Tuning Assets.
    /// </summary>
    [CreateAssetMenu(menuName = ConfigMenus.Root + "Tuning Profile", fileName = "TuningProfile")]
    public sealed class TuningProfile : ScriptableObject
    {
        [Header("Rules (affect outcomes)")]
        [SerializeField] private MatchRulesConfig _match;
        [SerializeField] private PathRulesConfig _paths;
        [SerializeField] private DamageConfig _damage;
        [SerializeField] private HitZoneConfig _hitZones;
        [SerializeField] private ClashConfig _clash;
        [SerializeField] private ElectricWallConfig _electricWall;
        [SerializeField] private ArenaConfig _arena;
        [SerializeField] private LoadoutRulesConfig _loadout;
        [SerializeField] private SignatureRulesConfig _signature;
        [SerializeField] private SuddenDeathConfig _suddenDeath;
        [SerializeField] private List<WeaponDefinition> _weapons = new List<WeaponDefinition>();
        [SerializeField] private List<BodyMoveDefinition> _bodyMoves = new List<BodyMoveDefinition>();
        [SerializeField] private List<SignatureMoveDefinition> _signatureMoves = new List<SignatureMoveDefinition>();

        [Header("Simulation and economy")]
        [SerializeField] private SimulationConfig _simulation;
        [SerializeField] private EconomyConfig _economy;

        [Header("Client only (never affect outcomes)")]
        [SerializeField] private InputConfig _input;
        [SerializeField] private ArenaSpaceConfig _arenaSpace;
        [SerializeField] private FeelConfig _feel;
        [SerializeField] private BalanceTargetsConfig _balanceTargets;

        public InputConfig InputConfig => _input;

        /// <summary>The live simulation asset (debug tools switch the weapon driver and segmentation on it).</summary>
        public SimulationConfig SimulationConfig => _simulation;
        public FeelConfig Feel => _feel;
        public BalanceTargetsConfig BalanceTargets => _balanceTargets;

        public ArenaSpace ArenaSpace =>
            _arenaSpace != null ? _arenaSpace.CreateSpace() : new ArenaSpace(ArenaSpaceConfig.DefaultWorldUnitsPerArenaUnit);

        /// <summary>Copies every rules value into a new snapshot. Missing assets fall back to code defaults and are reported.</summary>
        public RulesSettings BuildRulesSettings(ICollection<string> issues)
        {
            var settings = new RulesSettings
            {
                Match = Snapshot(_match, nameof(_match), issues),
                Paths = Snapshot(_paths, nameof(_paths), issues),
                Damage = Snapshot(_damage, nameof(_damage), issues),
                HitZones = Snapshot(_hitZones, nameof(_hitZones), issues),
                Clash = Snapshot(_clash, nameof(_clash), issues),
                ElectricWall = Snapshot(_electricWall, nameof(_electricWall), issues),
                Arena = Snapshot(_arena, nameof(_arena), issues),
                Loadout = Snapshot(_loadout, nameof(_loadout), issues),
                Signature = Snapshot(_signature, nameof(_signature), issues),
                SuddenDeath = Snapshot(_suddenDeath, nameof(_suddenDeath), issues),
            };
            AddSnapshots(_weapons, settings.Weapons, nameof(_weapons), issues);
            AddSnapshots(_bodyMoves, settings.BodyMoves, nameof(_bodyMoves), issues);
            AddSnapshots(_signatureMoves, settings.SignatureMoves, nameof(_signatureMoves), issues);
            return settings;
        }

        public SimulationSettings BuildSimulationSettings(ICollection<string> issues) => Snapshot(_simulation, nameof(_simulation), issues);

        public EconomySettings BuildEconomySettings(ICollection<string> issues) => Snapshot(_economy, nameof(_economy), issues);

        /// <summary>Every settings asset that wraps an engine-free settings class, for the inspector and tuning panel.</summary>
        public IEnumerable<SettingsConfigBase> SettingsConfigs()
        {
            SettingsConfigBase[] singles =
            {
                _match, _paths, _damage, _hitZones, _clash, _electricWall, _arena, _loadout, _signature, _suddenDeath,
                _simulation, _economy,
            };
            foreach (SettingsConfigBase config in singles)
            {
                if (config != null) yield return config;
            }

            foreach (WeaponDefinition weapon in _weapons)
            {
                if (weapon != null) yield return weapon;
            }

            foreach (BodyMoveDefinition move in _bodyMoves)
            {
                if (move != null) yield return move;
            }

            foreach (SignatureMoveDefinition move in _signatureMoves)
            {
                if (move != null) yield return move;
            }
        }

        /// <summary>Unity-only tuning assets (input, scale, feel, targets).</summary>
        public IEnumerable<ScriptableObject> ClientConfigs()
        {
            ScriptableObject[] configs = { _input, _arenaSpace, _feel, _balanceTargets };
            foreach (ScriptableObject config in configs)
            {
                if (config != null) yield return config;
            }
        }

        private void OnValidate()
        {
            var ignoredMissingAssets = new List<string>();
            var issues = new List<string>();
            BuildRulesSettings(ignoredMissingAssets).Validate(issues);
            foreach (string issue in issues)
            {
                Debug.LogWarning($"[XRim] Tuning profile '{name}': {issue}", this);
            }
        }

        private static T Snapshot<T>(SettingsConfig<T> config, string fieldName, ICollection<string> issues) where T : class, new()
        {
            if (config != null) return config.CreateSnapshot();
            issues.Add($"Tuning profile slot '{fieldName}' is empty; using code defaults.");
            return new T();
        }

        private static void AddSnapshots<T>(IEnumerable<SettingsConfig<T>> source, List<T> target, string fieldName, ICollection<string> issues)
            where T : class, new()
        {
            foreach (SettingsConfig<T> config in source)
            {
                if (config == null) issues.Add($"Tuning profile list '{fieldName}' has an empty entry.");
                else target.Add(config.CreateSnapshot());
            }
        }

        /// <summary>Serialized field names, for Editor tools that assign assets through SerializedObject.</summary>
        public static class FieldNames
        {
            public const string Match = nameof(_match);
            public const string Paths = nameof(_paths);
            public const string Damage = nameof(_damage);
            public const string HitZones = nameof(_hitZones);
            public const string Clash = nameof(_clash);
            public const string ElectricWall = nameof(_electricWall);
            public const string Arena = nameof(_arena);
            public const string Loadout = nameof(_loadout);
            public const string Signature = nameof(_signature);
            public const string SuddenDeath = nameof(_suddenDeath);
            public const string Weapons = nameof(_weapons);
            public const string BodyMoves = nameof(_bodyMoves);
            public const string SignatureMoves = nameof(_signatureMoves);
            public const string Simulation = nameof(_simulation);
            public const string Economy = nameof(_economy);
            public const string Input = nameof(_input);
            public const string ArenaSpace = nameof(_arenaSpace);
            public const string Feel = nameof(_feel);
            public const string BalanceTargets = nameof(_balanceTargets);
        }
    }
}
