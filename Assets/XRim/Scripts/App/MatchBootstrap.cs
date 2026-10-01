using System;
using System.Collections.Generic;
using UnityEngine;
using XRim.Config;
using XRim.Core;
using XRim.Networking;
using XRim.Presentation.Arena;
using XRim.Presentation.Dummy;
using XRim.Presentation.Playback;
using XRim.Rules;
using XRim.Rules.Arena;
using XRim.Rules.Match;
using XRim.Rules.Planning;
using XRim.Rules.Settings;
using XRim.Simulation;
using XRim.Simulation.Execution;
using XRim.Simulation.Physics;
using XRim.Simulation.Recording;
using XRim.Simulation.Settings;
using XRim.Simulation.Unity2D;

namespace XRim.App
{
    /// <summary>
    /// Composition root of a match scene. It builds the settings snapshot and wires, with constructor injection:
    /// the hidden Unity physics world → <see cref="TurnSimulator"/> → <see cref="LocalTurnAuthority"/> → <see cref="ClientMatchFlow"/>
    /// → <see cref="TurnPlayback"/> on the visible dummies. The authority holds each next planning phase until playback has
    /// finished (<see cref="PlaybackHoldMode.ClientReport"/>), so the replay never eats planning time. One per scene; no
    /// singletons.
    /// Modes running now: <see cref="MatchMode.BotVsBot"/>, and <see cref="MatchMode.Sandbox"/> once the debug tools hand
    /// over their plan sources. Touch input arrives in Session 08 and hot-seat in Session 09.
    /// </summary>
    public sealed class MatchBootstrap : MonoBehaviour
    {
        private const string LeftDummyName = "LeftDummy";
        private const string RightDummyName = "RightDummy";

        [Tooltip("Root of all tuning. Create one with XRim/Setup/Create Default Tuning Assets.")]
        [SerializeField] private TuningProfile _tuning;

        [SerializeField] private MatchMode _mode = MatchMode.BotVsBot;

        [Tooltip("The placeholder dummy prefabs (XRim/Setup/Build Placeholder Dummies), one per segmentation (D2).")]
        [SerializeField] private Ragdoll _sixBodies;

        [SerializeField] private Ragdoll _tenBodies;

        [Tooltip("Parent of every arena visual (the camera director flips it to mirror the view).")]
        [SerializeField] private Transform _arenaRoot;

        [Tooltip("Placeholder sprite for the floor and the edge markers.")]
        [SerializeField] private Sprite _square;

        [Tooltip("Seed for the bots and every random choice of the match. 0 = a new seed every match.")]
        [SerializeField] private int _randomSeed;

        private readonly ManualClock _clock = new ManualClock();
        private Unity2DPhysicsWorld _world;
        private RecordingTurnSimulator _simulator;
        private LocalTurnAuthority _authority;
        private ClientMatchFlow _flow;
        private PlanSourceBinder _binder;
        private TurnPlayback _playback;
        private PerSide<DummyView> _views;
        private RagdollSegmentation _viewSegmentation;
        private MatchSetup _setup;
        private bool _composed;
        private bool _started;

        public TuningProfile Tuning => _tuning;
        public MatchMode Mode => _mode;

        /// <summary>The snapshot the current match uses. Live tuning edits apply to the next snapshot.</summary>
        public RulesSettings RulesSettings { get; private set; }

        public SimulationSettings SimulationSettings { get; private set; }
        public RulePolicies Policies { get; private set; }
        public ArenaSpace Space { get; private set; }

        /// <summary>Parent of every arena visual; arena positions are its local space (debug drawing lines up with the dummies).</summary>
        public Transform ArenaRoot => _arenaRoot;

        /// <summary>The dummy prefabs the physics world spawns (debug tools build their own worlds from them).</summary>
        public RagdollPrefabSet RagdollPrefabs { get; private set; }

        /// <summary>Null until the scene has composed the match (Start).</summary>
        public ITurnAuthority Authority => _authority;

        public ClientMatchFlow Flow => _flow;
        public TurnPlayback Playback => _playback;
        public MatchSetup Setup => _setup;

        /// <summary>The last simulated turn's input (debug re-simulation); null before the first turn.</summary>
        public TurnInput LastTurnInput => _simulator?.LastInput;

        /// <summary>Wall-clock time the last simulation took (debug).</summary>
        public double LastSimulationMilliseconds => _simulator != null ? _simulator.LastSimulationMilliseconds : 0.0;

        /// <summary>The last turn re-simulated with the current tuning (display only); null if none.</summary>
        public TurnResult LastResimulation { get; private set; }

        /// <summary>True when composition worked and the match can start.</summary>
        public bool IsComposed => _composed;

        public bool HasStarted => _started;

        /// <summary>While true the authority's clock stands still, so the planning timer never runs out (Sandbox, debug cheats).</summary>
        public bool IsPlanningTimerFrozen { get; set; }

        private void Start()
        {
            if (!Compose()) return;

            switch (_mode)
            {
                case MatchMode.BotVsBot:
                    var random = new XorShiftRandom(_setup.RandomSeed);
                    StartMatch(PlanSourceFactory.Create(_mode, _setup, RulesSettings, null, null, random));
                    break;
                case MatchMode.Sandbox:
                    IsPlanningTimerFrozen = true;
                    Debug.Log("[XRim] Sandbox mode: waiting for the debug sandbox to plan both sides.", this);
                    break;
                default:
                    Debug.LogError($"[XRim] {_mode} is not playable yet (touch input arrives in Session 08, hot-seat in Session 09). " +
                                   "Set MatchBootstrap's mode to BotVsBot or Sandbox.", this);
                    break;
            }
        }

        /// <summary>Starts the match with these plan sources (one per side). Called once; the sandbox's debug tools call it.</summary>
        public void StartMatch(IReadOnlyList<IPlanSource> sources)
        {
            Guard.NotNull(sources, nameof(sources));
            if (!_composed) throw new InvalidOperationException("The match scene did not compose (see the Console).");
            if (_started) throw new InvalidOperationException("The match has already started.");

            _started = true;
            _binder = new PlanSourceBinder(_authority, sources);
            _authority.Start();
        }

        /// <summary>
        /// Debug: simulates the last turn again (same board and plans) with the current tuning and plays it. Display only:
        /// the match carries on from the original result. With unchanged tuning it repeats the original exactly.
        /// </summary>
        public bool ResimulateLastTurn()
        {
            TurnInput last = LastTurnInput;
            if (last == null) return false;

            var issues = new List<string>();
            RulesSettings rules = _tuning.BuildRulesSettings(issues);
            SimulationSettings simulation = _tuning.BuildSimulationSettings(issues);
            rules.Validate(issues);
            simulation.Validate(issues);
            foreach (string issue in issues)
            {
                Debug.LogWarning($"[XRim] Re-simulation: {issue}", _tuning);
            }

            LastResimulation = _simulator.Simulate(new TurnInput(last.Board, last.Plans, rules, simulation));
            Play(LastResimulation);
            return true;
        }

        /// <summary>Sandbox: each turn simulates with the simulation tuning as it is now (motor, masses, D1, D2...).</summary>
        private SimulationSettings LiveSimulationSettings()
        {
            var issues = new List<string>();
            SimulationSettings simulation = _tuning.BuildSimulationSettings(issues);
            simulation.Validate(issues);
            foreach (string issue in issues)
            {
                Debug.LogWarning($"[XRim] Sandbox turn: {issue}", _tuning);
            }

            return simulation;
        }

        private void Update()
        {
            if (!_started) return;
            float delta = Time.unscaledDeltaTime;
            if (!IsPlanningTimerFrozen) _clock.Advance(delta);
            _authority.Tick();
            _playback.Advance(delta);
        }

        private void OnDestroy()
        {
            if (_authority != null)
            {
                _authority.MatchStarted -= OnMatchStarted;
                _authority.PublicStateChanged -= OnPublicStateChanged;
                _authority.MatchEnded -= OnMatchEnded;
            }

            if (_flow != null) _flow.PhaseChanged -= OnPhaseChanged;
            if (_playback != null) _playback.Finished -= OnPlaybackFinished;
            _binder?.Dispose();
            _flow?.Dispose();
            _world?.Dispose();
            _world = null;
        }

        private bool Compose()
        {
            string missing = (_tuning == null ? " tuning profile" : string.Empty) + (_sixBodies == null ? " 6-body dummy" : string.Empty) +
                             (_tenBodies == null ? " 10-body dummy" : string.Empty) + (_arenaRoot == null ? " arena root" : string.Empty);
            if (missing.Length > 0)
            {
                Debug.LogError($"[XRim] MatchBootstrap is missing:{missing}. Run XRim/Setup/Create Sandbox Scene again.", this);
                return false;
            }

            // Gameplay physics runs in its own manually stepped scene; cosmetic physics is stepped by playback,
            // so slow motion slows debris too.
            Physics2D.simulationMode = SimulationMode2D.Script;

            var issues = new List<string>();
            RulesSettings = _tuning.BuildRulesSettings(issues);
            SimulationSettings = _tuning.BuildSimulationSettings(issues);
            RulesSettings.Validate(issues);
            SimulationSettings.Validate(issues);
            foreach (string issue in issues)
            {
                Debug.LogWarning($"[XRim] {issue}", _tuning);
            }

            Space = _tuning.ArenaSpace;
            Policies = new RulePolicies();
            _setup = CreateSetup(RulesSettings);
            RagdollPrefabs = new RagdollPrefabSet(_sixBodies, _tenBodies);
            _world = new Unity2DPhysicsWorld(Space, RagdollPrefabs);
            _simulator = new RecordingTurnSimulator(new TurnSimulator(_world, Policies));
            if (_mode == MatchMode.Sandbox) _simulator.SimulationSettingsOverride = LiveSimulationSettings;
            var options = new LocalTurnAuthorityOptions { PlaybackHold = new PlaybackHoldSettings { Mode = PlaybackHoldMode.ClientReport } };
            _authority = new LocalTurnAuthority(_setup, RulesSettings, SimulationSettings, Policies, _simulator, _clock, options);
            _flow = new ClientMatchFlow(_authority);
            ComposePresentation();

            _authority.MatchStarted += OnMatchStarted;
            _authority.PublicStateChanged += OnPublicStateChanged;
            _authority.MatchEnded += OnMatchEnded;
            _flow.PhaseChanged += OnPhaseChanged;
            _playback.Finished += OnPlaybackFinished;
            _composed = true;
            return true;
        }

        private void ComposePresentation()
        {
            ArenaEdges edges = Policies.ArenaEdge.EdgesFor(RulesSettings.Arena);
            ArenaView.Create(_arenaRoot, _square, edges, Space);
            BuildViews(SimulationSettings.Segmentation);
            _playback = new TurnPlayback(_views, Space);
        }

        private void BuildViews(RagdollSegmentation segmentation)
        {
            if (_views != null)
            {
                Destroy(_views.Left.gameObject);
                Destroy(_views.Right.gameObject);
            }

            Ragdoll prefab = segmentation == RagdollSegmentation.TenBodies ? _tenBodies : _sixBodies;
            _views = new PerSide<DummyView>(
                VisualDummyFactory.Create(prefab, _arenaRoot, LeftDummyName, RulesSettings.Weapons, Space),
                VisualDummyFactory.Create(prefab, _arenaRoot, RightDummyName, RulesSettings.Weapons, Space));
            _viewSegmentation = segmentation;
            _playback?.SetViews(_views);
        }

        /// <summary>Rebuilds the drawn dummies when a pose comes from the other segmentation (D2 switched in tuning).</summary>
        private void MatchViewsTo(PoseSnapshot pose)
        {
            RagdollSegmentation segmentation = pose.Left.HasLowerSegments ? RagdollSegmentation.TenBodies : RagdollSegmentation.SixBodies;
            if (segmentation != _viewSegmentation) BuildViews(segmentation);
        }

        private void Play(TurnResult result)
        {
            MatchViewsTo(result.FinalBoard.Pose);
            _playback.Play(result);
        }

        private void ShowBoard(BoardSnapshot board)
        {
            MatchViewsTo(board.Pose);
            _playback.ShowBoard(board);
        }

        /// <summary>Both dummies right-handed, each with the default loadout (D8, flagged in <see cref="LoadoutSettings"/>).</summary>
        private MatchSetup CreateSetup(RulesSettings rules)
        {
            var loadout = new List<WeaponId>();
            foreach (string id in rules.Loadout.DefaultLoadoutWeaponIds)
            {
                loadout.Add(new WeaponId(id));
            }

            uint seed = _randomSeed != 0 ? (uint)_randomSeed : (uint)Environment.TickCount | 1u;
            return new MatchSetup(PerSide<FighterSetup>.Create(_ => new FighterSetup(Handedness.Right, loadout)), seed);
        }

        private void OnMatchStarted(MatchStartInfo info) => ShowBoard(info.InitialBoard);

        private void OnPublicStateChanged(Side side, PublicPlanningState state) => _playback.ShowWeapon(side, state.Weapon);

        private void OnPhaseChanged(ClientPhase phase)
        {
            switch (phase)
            {
                case ClientPhase.Planning:
                    ShowBoard(_flow.CurrentWindow.Board);
                    break;
                case ClientPhase.Playback:
                    Play(_flow.CurrentResult);
                    break;
            }
        }

        private void OnPlaybackFinished(TurnResult result)
        {
            _flow.OnPlaybackFinished();
            _authority.NotifyPlaybackFinished(result.TurnIndex);

            // A replay or re-simulation watched during planning: show the board being planned from again.
            if (_flow.Phase == ClientPhase.Planning) ShowBoard(_flow.CurrentWindow.Board);
        }

        private void OnMatchEnded(MatchOutcome outcome) =>
            Debug.Log($"[XRim] Match over after {outcome.TurnCount} turns: {outcome.Winner} wins ({outcome.Reason}).", this);

        /// <summary>Serialized field names, for Editor tools that assign references through SerializedObject.</summary>
        public static class FieldNames
        {
            public const string Tuning = nameof(_tuning);
            public const string Mode = nameof(_mode);
            public const string SixBodies = nameof(_sixBodies);
            public const string TenBodies = nameof(_tenBodies);
            public const string ArenaRoot = nameof(_arenaRoot);
            public const string Square = nameof(_square);
        }
    }
}
