using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using XRim.Bots;
using XRim.Config;
using XRim.Core;
using XRim.Core.Gdd;
using XRim.Input;
using XRim.Networking;
using XRim.Presentation.Arena;
using XRim.Presentation.Audio;
using XRim.Presentation.Cameras;
using XRim.Presentation.Dummy;
using XRim.Presentation.Feel;
using XRim.Presentation.Moments;
using XRim.Presentation.Playback;
using XRim.Presentation.UI;
using XRim.Presentation.Vfx;
using XRim.Rules;
using XRim.Rules.Arena;
using XRim.Rules.Limbs;
using XRim.Rules.Match;
using XRim.Rules.Paths;
using XRim.Rules.Planning;
using XRim.Rules.Settings;
using XRim.Rules.Status;
using XRim.Simulation;
using XRim.Simulation.Execution;
using XRim.Simulation.Physics;
using XRim.Simulation.Recording;
using XRim.Simulation.Settings;
using XRim.Simulation.Unity2D;
using PanelSettings = UnityEngine.UIElements.PanelSettings;

namespace XRim.App
{
    /// <summary>
    /// Composition root of a match scene, with constructor injection and no singletons. Built once for the scene: the hidden Unity
    /// physics world, the visible dummies and <see cref="TurnPlayback"/> (with the <see cref="FeelDirector"/>'s hitstops and slow motion, and the camera director), the touch
    /// reader and, outside the sandbox, the planning HUD, the match HUD and the match screens. Built again for every match
    /// (<see cref="BeginMatch"/>, Rematch, a new mode), so tuning edits apply from the next match: the settings snapshot, the
    /// <see cref="RulePolicies"/> defaults, <see cref="LocalTurnAuthority"/> over the <see cref="TurnSimulator"/> → the
    /// <see cref="ClientMatchFlow"/> → playback, and who plans each side (<see cref="PlanSourceFactory"/>: the humans on this device
    /// through touch, the random debug bot).
    /// The authority holds each next planning phase until playback has finished (<see cref="PlaybackHoldMode.ClientReport"/>, reported
    /// by the flow), so the replay never eats planning time.
    /// Modes: the Match scene shows the mode picker (hot-seat through <see cref="HotSeatCoordinator"/> and its cover screen, vs the
    /// random bot, bot vs bot to watch; D25 dev opponents); the sandbox's debug tools plan both sides and hand their sources to
    /// <see cref="StartMatch"/>. Sudden death plays as the rules have it (GDD §14, Session 12): 1 HP, the first clean hit wins, a tie or a
    /// turn without a hit is played again a capped number of times, then the fallback decides; the electric walls are drawn on the board
    /// (<see cref="ElectricWallView"/>).
    /// </summary>
    public sealed class MatchBootstrap : MonoBehaviour
    {
        private const string LeftDummyName = "LeftDummy";
        private const string RightDummyName = "RightDummy";

        /// <summary>How many of the latest turns' simulation times the debug overlay's statistics keep (all-time numbers keep everything).</summary>
        private const int SimulationTimingWindowTurns = 200;

        /// <summary>How many played turns the debug pacing list keeps.</summary>
        private const int PacingHistoryTurns = 8;

        /// <summary>The human plays the left dummy against the bot.</summary>
        private const Side HumanSide = Side.Left;

        /// <summary>
        /// Mixed into the seed of the bots' loadout picks, so they never repeat the random numbers the same seed gives the bots' plans
        /// (technical, not a rule).
        /// </summary>
        private const uint LoadoutPickSeedSalt = 0x5EED10ADu;

        [Tooltip("Root of all tuning. Create one with XRim/Setup/Create Default Tuning Assets.")]
        [SerializeField] private TuningProfile _tuning;

        [Tooltip("The mode started at Play, unless the mode picker is shown.")]
        [SerializeField] private MatchMode _mode = MatchMode.BotVsBot;

        [Tooltip("Show the mode picker at Play (the Match scene) instead of starting the mode above.")]
        [SerializeField] private bool _pickModeAtStart;

        [Tooltip("The placeholder dummy prefabs (XRim/Setup/Build Placeholder Dummies), one per segmentation (D2).")]
        [SerializeField] private Ragdoll _sixBodies;

        [SerializeField] private Ragdoll _tenBodies;

        [Tooltip("The HUDs' panel settings asset (XRim/Setup/Create HUD Panel Settings; Create Match Scene wires it). UI Toolkit's text needs " +
                 "the data an asset carries: panel settings made in code have none, so in a build without this no HUD text would draw.")]
        [SerializeField] private PanelSettings _hudPanelSettings;

        [Tooltip("Parent of every arena visual (the camera director flips it to mirror the view).")]
        [SerializeField] private Transform _arenaRoot;

        [Tooltip("Placeholder sprite for the floor and the edge markers.")]
        [SerializeField] private Sprite _square;

        [Tooltip("The camera showing the arena, which touches are mapped through. Empty: the main camera.")]
        [SerializeField] private Camera _camera;

        [Tooltip("Flips the arena so a player's own dummy is on their side of the screen (GDD §4). Empty: the one on the camera.")]
        [SerializeField] private FixedSideCameraDirector _cameraDirector;

        [Tooltip("Seed for the bots and every random choice of the match. 0 = a new seed every match.")]
        [SerializeField] private int _randomSeed;

        [Tooltip("Bot vs bot: how long the frozen board stays on screen between turns, so it can be read. A viewing aid, not a rule.")]
        [Placeholder("Viewing aid for watching bot vs bot; never affects a match")]
        [SerializeField] private float _botWatchPauseSeconds = 1f;

        private readonly ManualClock _clock = new ManualClock();
        private readonly RollingStatistics _simulationTiming = new RollingStatistics(SimulationTimingWindowTurns);
        private readonly List<PlayedTurnPacing> _recentPacing = new List<PlayedTurnPacing>();
        private readonly TouchBlockerSet _touchBlockers = new TouchBlockerSet();
        private readonly IHandednessLayoutPolicy _handedness = new MirrorWhenLeftHanded();
        private readonly List<TouchSample> _discardedTouches = new List<TouchSample>();

        // Scene, built once.
        private Unity2DPhysicsWorld _world;
        private TurnPlayback _playback;
        private FeelDirector _feel;
        private MatchVfx _vfx;
        private MatchAudio _audio;
        private FeelConfig _feelConfig;
        private InstantReplay _replay;
        private TurnResult _replayedTurn;
        private bool _replayHoldsTurn;
        private bool _infiniteInk;
        private ScriptableObject _ownedAudio;
        private ElectricWallView _wallView;
        private InputConfig _input;
        private ScriptableObject _ownedInput;
        private ScriptableObject _ownedFeel;
        private ScriptableObject _ownedDisplay;
        private int _previousTargetFrameRate;
        private int _previousVSyncCount;
        private bool _displayApplied;
        private TouchInputReader _touchReader;
        private Material _lineMaterial;
        private PerSide<DummyView> _views;
        private RagdollSegmentation _viewSegmentation;
        private GameObject _arenaView;
        private PlanningHud _planningHud;
        private MatchHud _matchHud;
        private MatchScreens _screens;
        private LoadoutPickPresenter _loadoutPick;
        private PerSide<IReadOnlyList<WeaponId>> _lastPicks;
        private bool _snapshotUnused;
        private bool _composed;

        // Match, built per match.
        private RecordingTurnSimulator _simulator;
        private ScriptedSeverContactHandler _severCheat;
        private ScriptedKnockOutContactHandler _knockOutCheat;
        private ScriptedStatusContactHandler _statusCheat;
        private LocalTurnAuthority _authority;
        private ClientMatchFlow _flow;
        private PlanSourceBinder _binder;
        private MatchSetup _setup;
        private LocalPlayers _humans;
        private PlanningHudPresenter _planningPresenter;
        private MatchHudPresenter _matchHudPresenter;
        private PerSide<string> _names;
        private HotSeatCoordinator _hotSeat;
        private HotSeatStage _shownHotSeatStage;
        private Side _shownHotSeatSide;
        private bool _started;
        private bool _viewFlipped;
        private double _matchStartedRealSeconds;
        private bool _playbackReportPending;
        private double _playbackReportAtRealSeconds;

        public TuningProfile Tuning => _tuning;

        /// <summary>The mode of the current (or last) match.</summary>
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

        /// <summary>The current match's authority; null before a match is prepared (the mode picker).</summary>
        public ITurnAuthority Authority => _authority;

        public ClientMatchFlow Flow => _flow;

        /// <summary>The severs the force-sever cheat queued for the next turn (debug); empty between matches.</summary>
        public IReadOnlyList<SeveredLimbRef> QueuedSevers =>
            _severCheat != null ? _severCheat.Queued : (IReadOnlyList<SeveredLimbRef>)Array.Empty<SeveredLimbRef>();

        /// <summary>The dummies the force-double-KO cheat knocks out at the next turn's start (debug); empty between matches.</summary>
        public IReadOnlyList<Side> QueuedKnockOuts =>
            _knockOutCheat != null ? _knockOutCheat.Queued : (IReadOnlyList<Side>)Array.Empty<Side>();

        /// <summary>The stuns and staggers the force-stun cheat gives at the next turn's start (debug); empty between matches.</summary>
        public IReadOnlyList<(Side Side, StatusKind Kind)> QueuedStatuses =>
            _statusCheat != null ? _statusCheat.Queued : (IReadOnlyList<(Side, StatusKind)>)Array.Empty<(Side, StatusKind)>();

        public TurnPlayback Playback => _playback;

        /// <summary>The feel layer's pacing (hitstops, slow motion); debug tools read its plan for the turn being played.</summary>
        public FeelDirector Feel => _feel;

        /// <summary>The mechanical effects (debris, sparks, trails); tests count its pieces.</summary>
        public MatchVfx Vfx => _vfx;

        /// <summary>The match's sounds and music; tests read what it played.</summary>
        public MatchAudio Audio => _audio;

        /// <summary>The instant replay of a legendary turn, or of the last turn when the debug tools ask for it.</summary>
        public InstantReplay Replay => _replay;

        public MatchSetup Setup => _setup;

        /// <summary>The humans planning on this device in the current match; null in the sandbox and between matches.</summary>
        public LocalPlayers Humans => _humans;

        /// <summary>The last simulated turn's input (debug re-simulation); null before the first turn.</summary>
        public TurnInput LastTurnInput => _simulator?.LastInput;

        /// <summary>Wall-clock time the last simulation took (debug).</summary>
        public double LastSimulationMilliseconds => _simulator != null ? _simulator.LastSimulationMilliseconds : 0.0;

        /// <summary>
        /// Every simulated turn's wall-clock cost in milliseconds since the scene started, across matches (GDD §18: the hidden simulation's
        /// time per turn on this device). Measured by a stopwatch here in the App layer, never inside the rules.
        /// </summary>
        public RollingStatistics SimulationTiming => _simulationTiming;

        /// <summary>
        /// The last played turns' time on screen against the feel layer's plan, oldest first (Session 13: the debug Playback tab's pacing
        /// list; the playback hold is measured from these). Replays are not counted.
        /// </summary>
        public IReadOnlyList<PlayedTurnPacing> RecentPacing => _recentPacing;

        /// <summary>The last turn re-simulated with the current tuning (display only); null if none.</summary>
        public TurnResult LastResimulation { get; private set; }

        /// <summary>True once the scene is built (world, dummies, playback); a match may not be prepared yet.</summary>
        public bool IsComposed => _composed;

        /// <summary>True once the current match has started (its plan sources are bound).</summary>
        public bool HasStarted => _started;

        /// <summary>The authority's clock: the time the planning deadline is on, for the HUD's countdown and lock-out.</summary>
        public double AuthorityNowSeconds => _clock.NowSeconds;

        /// <summary>While true the authority's clock stands still, so the planning timer never runs out (Sandbox, debug cheats).</summary>
        public bool IsPlanningTimerFrozen { get; set; }

        /// <summary>
        /// Debug (the Cheats tab's infinite ink, Session 13): no path costs ink, for both sides, from now on and in later matches until it is
        /// switched off. Reach and the execution hard cap still limit a path, and a too-sharp spear turn still breaks it. Never a game rule.
        /// </summary>
        public bool InfiniteInk
        {
            get => _infiniteInk;
            set
            {
                _infiniteInk = value;
                if (Policies != null) ApplyInfiniteInk(Policies);
            }
        }

        /// <summary>Real seconds since the current match started (planning and playback included).</summary>
        public double MatchRealSeconds => _started ? Time.unscaledTimeAsDouble - _matchStartedRealSeconds : 0.0;

        /// <summary>
        /// Wires a scene built in code (tests, tools) before its first frame, as XRim/Setup/Create Match Scene does in the Editor.
        /// </summary>
        /// <param name="camera">The match camera (its <see cref="FixedSideCameraDirector"/>, if any, directs it); null finds the main camera.</param>
        public void Configure(TuningProfile tuning, Ragdoll sixBodies, Ragdoll tenBodies, Transform arenaRoot, Sprite square,
            MatchMode mode, bool pickModeAtStart, PanelSettings hudPanelSettings = null, Camera camera = null)
        {
            if (_composed) throw new InvalidOperationException("Configure the match scene before it starts.");
            _hudPanelSettings = hudPanelSettings;
            if (camera != null)
            {
                _camera = camera;
                _cameraDirector = camera.GetComponent<FixedSideCameraDirector>();
            }

            _tuning = tuning;
            _sixBodies = sixBodies;
            _tenBodies = tenBodies;
            _arenaRoot = arenaRoot;
            _square = square;
            _mode = mode;
            _pickModeAtStart = pickModeAtStart;
        }

        /// <summary>A screen area that belongs to something else (the debug overlay): touches landing there never draw or swipe.</summary>
        public void AddTouchBlocker(ITouchBlocker blocker) => _touchBlockers.Add(blocker);

        public void RemoveTouchBlocker(ITouchBlocker blocker) => _touchBlockers.Remove(blocker);

        private void Start()
        {
            if (!ComposeScene()) return;

            if (_mode == MatchMode.Sandbox)
            {
                PrepareMatch(MatchMode.Sandbox);
                IsPlanningTimerFrozen = true;
                Debug.Log("[XRim] Sandbox mode: waiting for the debug sandbox to plan both sides.", this);
            }
            else if (_pickModeAtStart)
            {
                ShowModePicker();
            }
            else
            {
                BeginMatch(_mode);
            }
        }

        private void Update()
        {
            if (!_composed) return;

            float delta = Time.unscaledDeltaTime;
            if (_started)
            {
                // The clock stands still while it is frozen (debug) and while a replay plays: a replay shows a turn that is over, so it must
                // never eat planning time or run out the wait for the playback report.
                bool clockStopped = IsPlanningTimerFrozen || _replay.IsPlaying;
                if (_hotSeat != null) _hotSeat.Advance(clockStopped ? 0.0 : delta); // hot-seat runs the planning timer per player
                else if (!clockStopped) _clock.Advance(delta);
                _humans?.TickInput();
                _authority.Tick();
            }

            _playback.Advance(delta);
            _replay.Advance();
            ReportPlaybackAfterWatchPause();
            SyncHotSeat();
            TickPresenters();
        }

        private void OnDestroy()
        {
            TearDownMatch();
            if (_playback != null)
            {
                _playback.Finished -= OnPlaybackFinished;
                _playback.RemoveReactor(_feel);
                if (_cameraDirector != null) _playback.RemoveReactor(_cameraDirector);
                if (_audio != null) _playback.RemoveReactor(_audio);
                if (_replay != null) _replay.Finished -= OnReplayFinished;
                if (_wallView != null) _playback.PoseShown -= _wallView.Show;
            }

            if (_wallView != null) Destroy(_wallView.gameObject);

            if (_screens != null)
            {
                _screens.ModePicked -= OnModePicked;
                _screens.CoverConfirmed -= OnCoverConfirmed;
                _screens.RematchPressed -= Rematch;
                _screens.ChangeModePressed -= ShowModePicker;
                _screens.ReplaySkipPressed -= OnReplaySkipPressed;
                Destroy(_screens.gameObject);
            }

            if (_planningHud != null) Destroy(_planningHud.gameObject);
            if (_matchHud != null) Destroy(_matchHud.gameObject);
            if (_ownedInput != null) Destroy(_ownedInput);
            if (_ownedFeel != null) Destroy(_ownedFeel);
            if (_ownedAudio != null) Destroy(_ownedAudio);
            if (_ownedDisplay != null) Destroy(_ownedDisplay);
            RestoreDisplay();
            if (_lineMaterial != null) Destroy(_lineMaterial);
            _world?.Dispose();
            _world = null;
        }

        // ----- matches -----

        /// <summary>
        /// Starts a new match in this mode with its own plan sources (humans on this device, bots). Ends the current one.
        /// </summary>
        /// <param name="loadouts">What both sides bring (the pre-match pick, <see cref="BeginLoadoutPick"/>); null: the default loadout for both.</param>
        public void BeginMatch(MatchMode mode, LoadoutReveal loadouts = null)
        {
            PrepareMatch(mode, loadouts);
            var random = new XorShiftRandom(_setup.RandomSeed);
            IReadOnlyList<IPlanSource> sources = PlanSourceFactory.Create(mode, _setup, RulesSettings, side => _humans.Add(side), random, HumanSide);

            // Hot-seat: it must follow the authority before the plan sources are bound, so planning opens behind the cover.
            if (mode == MatchMode.HotSeat)
            {
                _hotSeat = new HotSeatCoordinator(_authority, _clock,
                    new PerSide<TouchPlanSource>(_humans.Find(Side.Left), _humans.Find(Side.Right)));
            }

            StartMatch(sources);
        }

        /// <summary>The same mode again, from a fresh snapshot of the tuning; the players pick their loadouts again, from their last picks.</summary>
        public void Rematch() => BeginLoadoutPick(_mode);

        /// <summary>
        /// The pre-match pick (GDD §6, Session 14): each human on this device locks in 3 of the weapons they own (behind a cover screen
        /// in hot-seat), the bots pick at random, then both loadouts are revealed and the match starts with them. Without match screens
        /// (the sandbox, a scene built in code) the match starts at once with the default loadout.
        /// </summary>
        public void BeginLoadoutPick(MatchMode mode)
        {
            if (!_composed) throw new InvalidOperationException("The match scene did not compose (see the Console).");
            if (_screens == null || mode == MatchMode.Sandbox)
            {
                BeginMatch(mode);
                return;
            }

            // The picker and the match built from it read the same snapshot, so a tuning edit never lands between the two.
            TearDownMatch();
            _mode = mode;
            if (!_snapshotUnused) TakeSnapshot();
            _snapshotUnused = true;

            var ownership = new AllWeaponsOwned(); // Session 18: the profile's owned weapons
            if (!LoadoutOptions.CanFormALoadout(RulesSettings, ownership))
            {
                Debug.LogWarning("[XRim] The weapons cannot make a valid loadout (3 pickable weapons, a shield if one is required): " +
                                 "starting with the default loadout.", this);
                BeginMatch(mode);
                return;
            }

            if (_planningHud != null) _planningHud.SetVisible(false);
            if (_matchHud != null) _matchHud.SetVisible(false);
            if (_audio != null) _audio.SetMusicPlaying(false);
            SetViewFlipped(false);

            uint seed = _randomSeed != 0 ? (uint)_randomSeed ^ LoadoutPickSeedSalt : (uint)Environment.TickCount | 1u;
            _loadoutPick = LoadoutPickPresenter.Create(_screens, RulesSettings, ownership, mode, HumanSide, new RandomLoadoutPicker(),
                new XorShiftRandom(seed), _lastPicks);
            _loadoutPick.Completed += OnLoadoutsPicked;
            _loadoutPick.Begin();
        }

        private void OnLoadoutsPicked(LoadoutReveal loadouts)
        {
            _lastPicks = new PerSide<IReadOnlyList<WeaponId>>(loadouts.Left, loadouts.Right);
            DisposeLoadoutPick();
            BeginMatch(_mode, loadouts);
        }

        private void DisposeLoadoutPick()
        {
            if (_loadoutPick == null) return;
            _loadoutPick.Completed -= OnLoadoutsPicked;
            _loadoutPick.Dispose();
            _loadoutPick = null;
        }

        /// <summary>Ends the current match and shows the mode picker.</summary>
        public void ShowModePicker()
        {
            TearDownMatch();
            if (_audio != null) _audio.SetMusicPlaying(false);
            if (_screens == null) return;

            if (_planningHud != null) _planningHud.SetVisible(false);
            if (_matchHud != null) _matchHud.SetVisible(false);
            SetViewFlipped(false);
            var options = new List<MenuOption>();
            foreach (MatchMode mode in MatchLabels.PickerModes())
            {
                options.Add(MatchLabels.Option(mode));
            }

            _screens.ShowModePicker(MatchLabels.PickerTitle, MatchLabels.PickerNote, options);
        }

        /// <summary>
        /// Builds a match in this mode without plan sources yet: a fresh settings snapshot and policies, the authority and the flow.
        /// <see cref="StartMatch"/> then binds the sources (the sandbox's debug tools, or a test's scripted ones). Ends the current match.
        /// </summary>
        public void PrepareMatch(MatchMode mode, LoadoutReveal loadouts = null)
        {
            if (!_composed) throw new InvalidOperationException("The match scene did not compose (see the Console).");
            TearDownMatch();

            _mode = mode;
            if (!_snapshotUnused) TakeSnapshot();
            _snapshotUnused = false;
            ShowArena();
            BuildViews(SimulationSettings.Segmentation);

            _setup = CreateSetup(RulesSettings, loadouts);
            // The contact handler is the game's (hits, clashes, blocks), wrapped so the debug cheats can knock both dummies out, cut a limb or
            // stun a dummy at a turn's start, through the real paths (in that order: a dummy knocked out gets no stun).
            _statusCheat = new ScriptedStatusContactHandler(new HitContactHandler());
            _severCheat = new ScriptedSeverContactHandler(_statusCheat);
            _knockOutCheat = new ScriptedKnockOutContactHandler(_severCheat);
            var simulatorOptions = new TurnSimulatorOptions { Contacts = _knockOutCheat };
            _simulator = new RecordingTurnSimulator(new TurnSimulator(_world, Policies, simulatorOptions), _simulationTiming);
            if (mode == MatchMode.Sandbox)
            {
                _simulator.SimulationSettingsOverride = LiveSimulationSettings;
                _simulator.BodyMovesOverride = LiveBodyMoves;
            }

            var options = new LocalTurnAuthorityOptions { PlaybackHold = new PlaybackHoldSettings { Mode = PlaybackHoldMode.ClientReport } };
            _authority = new LocalTurnAuthority(_setup, RulesSettings, SimulationSettings, Policies, _simulator, _clock, options);

            // The flow follows the authority before any plan source is bound, so it sees each planning phase open before it locks.
            _flow = new ClientMatchFlow(_authority);
            _authority.MatchStarted += OnMatchStarted;
            _authority.PublicStateChanged += OnPublicStateChanged;
            _flow.PhaseChanged += OnPhaseChanged;
            if (mode == MatchMode.Sandbox) return;

            _names = MatchLabels.FighterNames(mode, HumanSide);
            _humans = new LocalPlayers(CreateTouchSource, CreatePreview);
            if (_planningHud != null)
            {
                _planningPresenter = new PlanningHudPresenter(_planningHud, _authority, _setup, RulesSettings.Match);
                _planningHud.SetVisible(false);
            }

            if (_matchHud != null) _matchHudPresenter = new MatchHudPresenter(_matchHud, _flow, RulesSettings, _names);
        }

        /// <summary>Starts the prepared match with these plan sources (one per side). The sandbox's debug tools call it.</summary>
        public void StartMatch(IReadOnlyList<IPlanSource> sources)
        {
            Guard.NotNull(sources, nameof(sources));
            if (!_composed || _authority == null) throw new InvalidOperationException("No match is prepared (see the Console).");
            if (_started) throw new InvalidOperationException("The match has already started.");

            _started = true;
            _binder = new PlanSourceBinder(_authority, sources);
            _matchStartedRealSeconds = Time.unscaledTimeAsDouble;
            DiscardPendingTouches(); // the tap that started the match is not a stroke
            if (_screens != null) _screens.Hide();
            if (_matchHud != null) _matchHud.SetVisible(true);
            _audio.SetMusicPlaying(true);
            TouchPlanSource human = _hotSeat == null && _humans != null ? _humans.Find(HumanSide) : null;
            SetViewFlipped(human != null && human.Layout.ArenaViewFlipped);
            _authority.Start();
            SyncHotSeat();
        }

        private void TearDownMatch()
        {
            DisposeLoadoutPick();
            _playbackReportPending = false;
            StopReplay();

            _shownHotSeatStage = HotSeatStage.Idle;
            if (_authority == null) return;

            _authority.MatchStarted -= OnMatchStarted;
            _authority.PublicStateChanged -= OnPublicStateChanged;
            _flow.PhaseChanged -= OnPhaseChanged;
            _hotSeat?.Dispose();
            _binder?.Dispose();
            _flow.Dispose();
            _planningPresenter?.Dispose();
            _humans?.Dispose();
            _binder = null;
            _flow = null;
            _planningPresenter = null;
            _matchHudPresenter = null;
            _humans = null;
            _hotSeat = null;
            _authority = null;
            _simulator = null;
            _severCheat = null;
            _knockOutCheat = null;
            _statusCheat = null;
            _started = false;
        }

        /// <summary>
        /// Debug (the Cheats tab's force sever): cuts this arm or leg at the very start of the next turn, through the same path as a third
        /// hit (it falls, a held weapon drops, the events and debris play). Does nothing between matches.
        /// </summary>
        public void QueueSever(Side side, BodyPart part) => _severCheat?.QueueSever(side, part);

        /// <summary>
        /// Debug (the Cheats tab's force double KO, Session 12): both dummies die at the very start of the next turn through the real death
        /// path, so a normal turn goes to sudden death (§3) and a sudden-death turn is a tie (§14). Does nothing between matches.
        /// </summary>
        public void QueueDoubleKnockOut() => _knockOutCheat?.QueueDoubleKnockOut();

        /// <summary>
        /// Debug (the Cheats tab's force KO, Session 13): this dummy dies at the very start of the next turn through the real death path, so
        /// the other wins (in sudden death too): the match-winning KO and its replay on demand. Does nothing between matches.
        /// </summary>
        public void QueueKnockOut(Side side) => _knockOutCheat?.QueueKnockOut(side);

        /// <summary>
        /// Debug (the Cheats tab's force stun, Session 13): this dummy is stunned or staggered at the very start of the next turn, through the
        /// real status path, so the turn after it is planned under that status (D17: no body move by default). Does nothing between matches.
        /// </summary>
        public void QueueStatus(Side side, StatusKind kind) => _statusCheat?.QueueStatus(side, kind);

        /// <summary>
        /// Debug (the Cheats tab's turn counter, Session 12): the next turn played is this one (0-based), so the turn cap can be reached at
        /// once. Works between turns and while planning; returns false when it cannot (no match, or the match is over).
        /// </summary>
        public bool SetTurnCounter(int turnIndex)
        {
            if (_authority == null || turnIndex < 0) return false;
            MatchPhase phase = _authority.Phase;
            if (phase != MatchPhase.TurnStart && phase != MatchPhase.SuddenDeathSetup && phase != MatchPhase.Planning) return false;
            _authority.SetTurnIndexForDebug(turnIndex);
            return true;
        }

        // ----- debug -----

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

        /// <summary>True when <see cref="ReplayLastTurn"/> can play: between turns (planning), nothing playing, no hot-seat cover up.</summary>
        public bool CanReplayLastTurn =>
            _composed && _flow != null && _flow.Phase == ClientPhase.Planning && _playback.Current != null && !_playback.IsPlaying &&
            !_replay.IsPlaying && (_hotSeat == null || _hotSeat.Stage != HotSeatStage.Cover);

        /// <summary>
        /// Debug (the Playback tab, Session 13): plays the last turn's instant replay now, as a legendary turn's replay plays (its moment, or
        /// its hardest hit when it has none). The planning clock stands still meanwhile, and the board being planned comes back after it.
        /// </summary>
        public bool ReplayLastTurn()
        {
            if (!CanReplayLastTurn) return false;
            TurnResult turn = _playback.Current;
            BeginReplay(turn, RequestedMoment.Pick(turn, new HeuristicMomentDetector(MomentSettings.From(_feelConfig))), false);
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

        /// <summary>Sandbox: each turn plays its body moves with the body-move tuning as it is now (GDD §5 distances, D12, D13).</summary>
        private List<BodyMoveStats> LiveBodyMoves()
        {
            var issues = new List<string>();
            RulesSettings rules = _tuning.BuildRulesSettings(issues);
            rules.Validate(issues);
            foreach (string issue in issues)
            {
                Debug.LogWarning($"[XRim] Sandbox turn: {issue}", _tuning);
            }

            return rules.BodyMoves;
        }

        // ----- scene composition -----

        private bool ComposeScene()
        {
            string missing = (_tuning == null ? " tuning profile" : string.Empty) + (_sixBodies == null ? " 6-body dummy" : string.Empty) +
                             (_tenBodies == null ? " 10-body dummy" : string.Empty) + (_arenaRoot == null ? " arena root" : string.Empty);
            if (missing.Length > 0)
            {
                Debug.LogError($"[XRim] MatchBootstrap is missing:{missing}. Run XRim/Setup/Create Match Scene (or Create Sandbox Scene) again.", this);
                return false;
            }

            // Gameplay physics runs in its own manually stepped scene; cosmetic physics is stepped by playback,
            // so slow motion slows debris too.
            Physics2D.simulationMode = SimulationMode2D.Script;

            TakeSnapshot();
            _snapshotUnused = true;
            Space = _tuning.ArenaSpace;
            RagdollPrefabs = new RagdollPrefabSet(_sixBodies, _tenBodies);
            _world = new Unity2DPhysicsWorld(Space, RagdollPrefabs);
            ShowArena();
            BuildViews(SimulationSettings.Segmentation);
            _playback = new TurnPlayback(_views, Space);
            _wallView = ElectricWallView.Create(_arenaRoot, _square, Space);
            _playback.PoseShown += _wallView.Show;
            FeelConfig feel = _tuning.Feel != null ? _tuning.Feel : OwnedDefault<FeelConfig>(ref _ownedFeel, "Feel");

            // The camera frames the duel and reacts to it (GDD §2, flagged); the feel layer holds each turn until it is still again.
            if (_camera == null) _camera = Camera.main;
            if (_cameraDirector == null && _camera != null) _cameraDirector = _camera.GetComponent<FixedSideCameraDirector>();
            if (_cameraDirector != null)
            {
                _cameraDirector.Configure(feel, Space, _playback);
                _playback.AddReactor(_cameraDirector);
            }

            _feel = new FeelDirector(feel, _cameraDirector);
            _feelConfig = feel;
            _playback.Pacer = _feel;
            _playback.AddReactor(_feel);
            _replay = new InstantReplay(_playback, _feel, _cameraDirector);
            _replay.Finished += OnReplayFinished;
            _touchBlockers.Add(new ReplayTouchBlocker(_replay));
            _vfx = gameObject.AddComponent<MatchVfx>();
            _vfx.Configure(_playback, _arenaRoot, Space, feel, () => SimulationSettings.GravityUnitsPerSecondSquared, weapon => RulesSettings.FindWeapon(weapon));
            _playback.AddReactor(_vfx);
            ComposeAudio(feel);
            _playback.Finished += OnPlaybackFinished;

            _input = _tuning.InputConfig != null ? _tuning.InputConfig : OwnedDefault<InputConfig>(ref _ownedInput, "Input");
            ApplyDisplay(_tuning.Display != null ? _tuning.Display : OwnedDefault<DisplayConfig>(ref _ownedDisplay, "Display"));
            _touchReader = GetComponent<TouchInputReader>();
            if (_touchReader == null) _touchReader = gameObject.AddComponent<TouchInputReader>();
            if (_mode != MatchMode.Sandbox) ComposeMatchUi();

            _composed = true;
            return true;
        }

        /// <summary>
        /// The duel's sound (GDD §2, Session 13): every played event's sounds and the music loop, silent where no clip exists yet. In slow
        /// motion sounds play deeper (the feel layer's speed). The camera gets an audio listener if the scene has none on it.
        /// </summary>
        private void ComposeAudio(FeelConfig feel)
        {
            AudioConfig audio = _tuning.Audio != null ? _tuning.Audio : OwnedDefault<AudioConfig>(ref _ownedAudio, "Audio");
            _audio = gameObject.AddComponent<MatchAudio>();
            _audio.Configure(audio, feel, () => _feel.SlowMotion.SpeedAt(_playback.Player.CurrentTime));
            _playback.AddReactor(_audio);
            if (_camera != null && _camera.GetComponent<AudioListener>() == null) _camera.gameObject.AddComponent<AudioListener>();
        }

        /// <summary>
        /// The frame-rate target (GDD §18, a client setting: it never changes an outcome). Vertical sync is switched off, because it
        /// pins the frame rate to the screen's own and the target only applies without it. What was set before is put back when the
        /// scene goes, so the Editor is left as it was. Logs what the device reports, for the §18 record.
        /// </summary>
        private void ApplyDisplay(DisplayConfig display)
        {
            _previousTargetFrameRate = Application.targetFrameRate;
            _previousVSyncCount = QualitySettings.vSyncCount;
            _displayApplied = true;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = display.TargetFrameRate;

            Rect safe = Screen.safeArea;
            ScreenInsets insets = ScreenInsets.FromSafeArea(safe.x, safe.y, safe.width, safe.height, Screen.width, Screen.height);
            string target = display.TargetFrameRate == DisplayConfig.PlatformDefaultFrameRate ? "the platform default" : display.TargetFrameRate.ToString();
            Debug.Log($"[XRim] Display: {Screen.width}x{Screen.height} at {Screen.currentResolution.refreshRateRatio.value:0.#} Hz, frame-rate target " +
                      $"{target}, unsafe screen edges (pixels) {(insets.IsNone ? "none" : insets.ToString())}.", this);
        }

        private void RestoreDisplay()
        {
            if (!_displayApplied) return;
            _displayApplied = false;
            Application.targetFrameRate = _previousTargetFrameRate;
            QualitySettings.vSyncCount = _previousVSyncCount;
        }

        /// <summary>The HUDs and screens of a real match (the sandbox has its own panels).</summary>
        private void ComposeMatchUi()
        {
            if (_camera == null) Debug.LogWarning("[XRim] No camera for the match: touches cannot be mapped to the arena.", this);
            _touchReader.MouseActsAsTouch = Application.isEditor || !Application.isMobilePlatform; // a mouse plays as a finger
            _lineMaterial = ArenaLines.CreateMaterial();

            // Without the asset a player build draws no HUD text (UI Toolkit has no ICU data for panel settings made in code).
            if (_hudPanelSettings == null && !Application.isEditor)
            {
                Debug.LogError("[XRim] The match scene has no HUD panel settings asset, so no HUD text will draw on this device. " +
                               "Run XRim/Setup/Create Match Scene (it creates and wires it), then build again.", this);
            }

            Vector2Int reference = _input.ReferenceResolution;
            _planningHud = PlanningHud.Create(reference, _hudPanelSettings);
            _planningHud.SetVisible(false);
            _touchBlockers.Add(new HudTouchBlocker(_planningHud));
            _matchHud = MatchHud.Create(reference, _hudPanelSettings);
            _matchHud.SetVisible(false);
            _screens = MatchScreens.Create(reference, _hudPanelSettings);
            _screens.ModePicked += OnModePicked;
            _screens.CoverConfirmed += OnCoverConfirmed;
            _screens.RematchPressed += Rematch;
            _screens.ChangeModePressed += ShowModePicker;
            _screens.ReplaySkipPressed += OnReplaySkipPressed;
        }

        private T OwnedDefault<T>(ref ScriptableObject owned, string slot) where T : ScriptableObject
        {
            Debug.LogWarning($"[XRim] The tuning profile has no {slot} asset: using the code defaults.", _tuning);
            T instance = ScriptableObject.CreateInstance<T>();
            owned = instance;
            return instance;
        }

        /// <summary>A fresh snapshot of every rules and simulation value, and fresh policies (the defaults of every TBD seam).</summary>
        private void TakeSnapshot()
        {
            var issues = new List<string>();
            RulesSettings = _tuning.BuildRulesSettings(issues);
            SimulationSettings = _tuning.BuildSimulationSettings(issues);
            RulesSettings.Validate(issues);
            SimulationSettings.Validate(issues);
            foreach (string issue in issues)
            {
                Debug.LogWarning($"[XRim] {issue}", _tuning);
            }

            Policies = new RulePolicies();
            ApplyInfiniteInk(Policies);
        }

        private void ApplyInfiniteInk(RulePolicies policies)
        {
            if (_infiniteInk && !(policies.InkCost is FreeInkCostModel)) policies.InkCost = new FreeInkCostModel(policies.InkCost);
            else if (!_infiniteInk && policies.InkCost is FreeInkCostModel free) policies.InkCost = free.Inner;
        }

        /// <summary>The floor and edge markers for this match's arena settings.</summary>
        private void ShowArena()
        {
            if (_arenaView != null) Destroy(_arenaView);
            ArenaEdges edges = Policies.ArenaEdge.EdgesFor(RulesSettings.Arena);
            _arenaView = ArenaView.Create(_arenaRoot, _square, edges, Space);
            if (_cameraDirector != null) _cameraDirector.SetArena(edges);
        }

        /// <summary>The drawn dummies, holding this snapshot's weapons, for a segmentation (D2).</summary>
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

        /// <summary>
        /// Both dummies right-handed (a dev setting, D24), each with the loadout it picked, or the default loadout without a pick
        /// (D8, flagged in <see cref="LoadoutSettings"/>: the sandbox, scenes built in code).
        /// </summary>
        private MatchSetup CreateSetup(RulesSettings rules, LoadoutReveal loadouts)
        {
            var defaultLoadout = new List<WeaponId>();
            foreach (string id in rules.Loadout.DefaultLoadoutWeaponIds)
            {
                defaultLoadout.Add(new WeaponId(id));
            }

            uint seed = _randomSeed != 0 ? (uint)_randomSeed : (uint)Environment.TickCount | 1u;
            PerSide<FighterSetup> fighters = PerSide<FighterSetup>.Create(side =>
                new FighterSetup(Handedness.Right, loadouts != null ? loadouts.Of(side) : defaultLoadout));
            return new MatchSetup(fighters, seed);
        }

        // ----- humans on this device -----

        private TouchPlanSource CreateTouchSource(Side side)
        {
            // The swipe and tap numbers are read at the start of each match: edit them in the Input asset, then Rematch.
            SwipeSettings swipe = SwipeSettings.From(_input);
            DualZoneSettings zones = DualZoneSettings.From(_input);
            var scheme = new DualZoneInputScheme(_touchReader, new FourWaySwipeClassifier(swipe), swipe, zones, new ScreenToArenaMapper(),
                ReadView, _touchBlockers);
            return new TouchPlanSource(side, scheme, LayoutFor(side));
        }

        private PlanPreviewPresenter CreatePreview(Side side) => new PlanPreviewPresenter(_arenaRoot, _lineMaterial, side + "Preview",
            Policies, RulesSettings, SimulationSettings, Space, RulesSettings.FindBodyMove);

        /// <summary>The screen as a player of this side sees it; mirrored for a left-handed dev setting (D24).</summary>
        private ScreenLayout LayoutFor(Side side) => new ScreenLayout(Screen.width, Screen.height, _input.BodyZoneWidthFraction,
            _handedness.IsScreenMirrored(_input.DevHandedness), side);

        /// <summary>What the camera shows now, so touches map to the arena correctly even if the camera moves.</summary>
        private ArenaViewFrame ReadView() => CameraArenaView.Read(_camera, Space);

        /// <summary>Marks every touch so far as read, so a tap on a button that just closed never reaches a player's input.</summary>
        private void DiscardPendingTouches()
        {
            if (_touchReader == null || !_touchReader.isActiveAndEnabled) return;
            _touchReader.ReadInto(_discardedTouches);
            _discardedTouches.Clear();
        }

        /// <summary>The human the screen serves now (the planning HUD, the preview); null when none is planning.</summary>
        private TouchPlanSource ShownHuman()
        {
            if (_hotSeat != null) return _hotSeat.Stage == HotSeatStage.Planning ? _hotSeat.ActivePlayer : null;
            return _humans != null ? _humans.Find(HumanSide) : null;
        }

        /// <summary>
        /// Hot-seat on screen: the cover while the device changes hands (opaque, so nothing of the last plan shows), each player's own
        /// view while they plan (their dummy on their side, GDD §4), and the plain view (left player on the left) for playback.
        /// </summary>
        private void SyncHotSeat()
        {
            if (_hotSeat == null) return;
            HotSeatStage stage = _hotSeat.Stage;
            Side side = _hotSeat.ActiveSide;
            if (stage == _shownHotSeatStage && side == _shownHotSeatSide) return;
            _shownHotSeatStage = stage;
            _shownHotSeatSide = side;

            switch (stage)
            {
                case HotSeatStage.Cover:
                    SetViewFlipped(false);
                    PlanningWindow window = _flow.CurrentWindow;
                    float seconds = window != null ? window.Constraints[side].PlanningDurationSeconds : RulesSettings.Match.PlanningDurationSeconds;
                    if (_screens != null) _screens.ShowCover(MatchLabels.CoverPlayer(side), MatchLabels.CoverInstruction, MatchLabels.CoverAction(seconds));
                    break;
                case HotSeatStage.Planning:
                    if (_screens != null) _screens.Hide();
                    SetViewFlipped(_hotSeat.ActivePlayer.Layout.ArenaViewFlipped);
                    break;
                default:
                    if (_screens != null) _screens.Hide();
                    SetViewFlipped(false);
                    break;
            }
        }

        /// <summary>The player on the cover screen has the device: their planning and timer start; the tap itself is not a stroke.</summary>
        private void OnCoverConfirmed()
        {
            if (_hotSeat == null) return;
            _hotSeat.ConfirmCover();
            DiscardPendingTouches();
            SyncHotSeat();
        }

        private void SetViewFlipped(bool flipped)
        {
            _viewFlipped = flipped;
            if (_cameraDirector != null) _cameraDirector.SetArenaFlipped(flipped);
        }

        private void TickPresenters()
        {
            if (_humans != null)
            {
                TouchPlanSource shown = ShownHuman();
                _humans.UpdatePreviews(shown);
                if (_planningPresenter != null)
                {
                    _planningPresenter.Source = shown;
                    _planningPresenter.Tick(_clock.NowSeconds, shown != null ? _humans.PreviewOf(shown) : null);
                }
            }

            _matchHudPresenter?.Tick(_playback.IsPlaying ? _playback.Player.CurrentTime : (SimTime?)null, _viewFlipped);
        }

        // ----- playback and the flow -----

        private void Play(TurnResult result)
        {
            StopReplay(); // a turn played (or re-simulated) while a replay shows ends the replay
            MatchViewsTo(result.FinalBoard.Pose);
            _playback.Play(result);
        }

        private void ShowBoard(BoardSnapshot board)
        {
            MatchViewsTo(board.Pose);
            _playback.ShowBoard(board);
            if (_cameraDirector != null) _cameraDirector.ShowBoard(board.Pose);
            _audio.OnBoardShown(board.State);
            CoverDummiesWithBodyZones(board.Pose);
        }

        /// <summary>
        /// GDD §4 (Decided: the body zone covers the player's own dummy; designer 2026-10-11, F3, flagged in the Input asset): each human's
        /// body zone reaches from their screen edge to just past their own dummy on the frozen board, as the camera frames it now (the
        /// camera never moves while a player plans). Off: the plain strip.
        /// </summary>
        private void CoverDummiesWithBodyZones(PoseSnapshot pose)
        {
            if (_humans == null || _camera == null) return;
            ArenaViewFrame view = CameraArenaView.Read(_camera, Space);
            float centreXUnits = _viewFlipped ? -view.CenterUnits.X : view.CenterUnits.X; // the canonical arena, whichever way it is shown
            float widthUnits = view.VisibleHeightUnits * _camera.aspect;
            foreach (TouchPlanSource source in _humans.Sources)
            {
                float cover = _input.BodyZoneCoversDummy && widthUnits > 0f
                    ? BodyZoneCover.FromOwnEdgeFraction(pose.Get(source.Side), source.Side, centreXUnits, widthUnits, _input.BodyZoneMarginBeyondDummyUnits)
                    : 0f;
                source.Layout = source.Layout.WithDummyCover(cover);
            }
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
                    LogSimulatedTurn(_flow.CurrentResult);
                    Play(_flow.CurrentResult);
                    break;
                case ClientPhase.MatchOver:
                    ShowMatchOver(_flow.Outcome);
                    break;
            }
        }

        /// <summary>
        /// One log line per turn with what the simulation cost on this device (the numbers the GDD §18 performance budget needs; the debug
        /// tools keep the log lines in a file on the device). Culture-invariant, so it reads the same on every phone.
        /// </summary>
        private void LogSimulatedTurn(TurnResult turn)
        {
            TurnTimeline timeline = turn.Timeline;
            int steps = timeline.Frames.Count > 0 ? timeline.Frames[timeline.Frames.Count - 1].Step : 0;
            Debug.Log(string.Format(CultureInfo.InvariantCulture,
                "[XRim] Turn {0} ({1}) simulated in {2:0.0} ms: {3} steps, {4:0.000} s of playback, {5}. Simulation so far: {6}",
                turn.TurnIndex + 1, _mode, LastSimulationMilliseconds, steps, timeline.Duration.Seconds, turn.EndReason,
                _simulationTiming.Describe("ms")), this);
        }

        /// <summary>
        /// How long the turn took on screen with the feel effects (hitstops, slow motion, the camera settling) against its recorded length
        /// and the feel layer's plan: the numbers the playback hold (<see cref="PlaybackHoldSettings"/>) is measured from (Session 13).
        /// </summary>
        private void LogPlayedTurn(TurnResult turn)
        {
            var pacing = new PlayedTurnPacing(turn.TurnIndex, _feel.Pacing, _playback.PlayRealSeconds);
            if (_recentPacing.Count == PacingHistoryTurns) _recentPacing.RemoveAt(0);
            _recentPacing.Add(pacing);
            Debug.Log(string.Format(CultureInfo.InvariantCulture, "[XRim] Turn {0} ({1}) played in {2:0.00} s for {3:0.000} s of turn (planned {4}).",
                turn.TurnIndex + 1, _mode, pacing.OnScreenSeconds, turn.Timeline.Duration.Seconds, pacing.Planned), this);
        }

        private void OnPlaybackFinished(TurnResult result)
        {
            if (_flow == null || _replay.IsPlaying) return; // an instant replay ends itself
            LogPlayedTurn(result);
            if (TryStartReplay(result)) return;
            ContinueAfterPlayback();
        }

        /// <summary>
        /// A legendary turn just played (GDD pillar 1; §17: a local instant replay only, designer 2026-10-11): it plays again at once, slower,
        /// the camera on the moment, before the match moves on; a tap skips it. Every KO is replayed (the match-winning blow). Not in the
        /// sandbox, and never a turn twice.
        /// </summary>
        private bool TryStartReplay(TurnResult turn)
        {
            if (_mode == MatchMode.Sandbox || !_feelConfig.AutoReplayLegendaryTurns || _flow.Phase != ClientPhase.Playback || turn == _replayedTurn)
                return false;
            if (!new HeuristicMomentDetector(MomentSettings.From(_feelConfig)).TryDetect(turn, out LegendaryMoment moment)) return false;

            _replayedTurn = turn;
            BeginReplay(turn, moment, true);
            return true;
        }

        /// <param name="holdsTurn">True for a legendary turn's own replay: the match moves on when it ends.</param>
        private void BeginReplay(TurnResult turn, LegendaryMoment moment, bool holdsTurn)
        {
            _replayHoldsTurn = holdsTurn;
            string why = holdsTurn ? "legendary moment" : "replay asked for";
            Debug.Log($"[XRim] Turn {turn.TurnIndex + 1}: {why}, {moment}. Instant replay.", this);
            _replay.Begin(turn, moment, ReplaySettings.From(_feelConfig));
            if (_screens != null)
            {
                PerSide<string> names = _names ?? MatchLabels.FighterNames(_mode, HumanSide);
                _screens.ShowReplay(MatchLabels.ReplayTitle, MatchLabels.MomentCaption(moment.Kind, moment.Hero, names), MatchLabels.ReplayHint);
            }
        }

        private void OnReplaySkipPressed() => _replay.Skip();

        /// <summary>
        /// The replay is over: back to the board (a cut). A legendary turn's own replay then lets the match move on, as after any turn; a
        /// replay asked for between turns returns to the board being planned.
        /// </summary>
        private void OnReplayFinished()
        {
            if (_screens != null) _screens.Hide();
            bool heldTheTurn = _replayHoldsTurn;
            _replayHoldsTurn = false;
            if (_flow != null && _flow.Phase == ClientPhase.Planning)
            {
                ShowBoard(_flow.CurrentWindow.Board);
                return;
            }

            if (_replay.Turn != null) ShowBoard(_replay.Turn.FinalBoard);
            if (heldTheTurn && _flow != null && _flow.Phase == ClientPhase.Playback) ContinueAfterPlayback();
        }

        /// <summary>Ends a replay without the match moving on: a new playback replaces it, or the match is torn down.</summary>
        private void StopReplay()
        {
            _replayHoldsTurn = false;
            if (_replay == null || !_replay.IsPlaying) return;
            _replay.Cancel();
            if (_screens != null) _screens.Hide();
        }

        private void ContinueAfterPlayback()
        {
            // Watching two bots: the frozen board stays a moment before the next turn, so it can be read.
            if (_mode == MatchMode.BotVsBot && _flow.Phase == ClientPhase.Playback && _botWatchPauseSeconds > 0f)
            {
                _playbackReportPending = true;
                _playbackReportAtRealSeconds = Time.unscaledTimeAsDouble + _botWatchPauseSeconds;
                return;
            }

            ReportPlaybackFinished();
        }

        private void ReportPlaybackAfterWatchPause()
        {
            if (!_playbackReportPending || Time.unscaledTimeAsDouble < _playbackReportAtRealSeconds) return;
            _playbackReportPending = false;
            ReportPlaybackFinished();
        }

        private void ReportPlaybackFinished()
        {
            if (_flow == null) return;
            _flow.OnPlaybackFinished(); // also tells the authority, which then starts the next planning timer

            // A replay or re-simulation watched during planning: show the board being planned from again.
            if (_flow.Phase == ClientPhase.Planning) ShowBoard(_flow.CurrentWindow.Board);
        }

        private void ShowMatchOver(MatchOutcome outcome)
        {
            PerSide<string> names = _names ?? MatchLabels.FighterNames(_mode, HumanSide);
            string title = MatchOverText.Title(outcome, names);
            string reason = MatchOverText.Reason(outcome, names);
            string summary = MatchOverText.Summary(outcome.TurnCount, MatchRealSeconds);
            Debug.Log($"[XRim] Match over ({_mode}): {title} {reason}. {summary}.", this);
            _audio.SetMusicPlaying(false);
            if (_screens != null) _screens.ShowMatchOver(title, reason, summary);
        }

        private void OnModePicked(int index)
        {
            IReadOnlyList<MatchMode> modes = MatchLabels.PickerModes();
            if (index >= 0 && index < modes.Count) BeginLoadoutPick(modes[index]);
        }

        /// <summary>Serialized field names, for Editor tools that assign references through SerializedObject.</summary>
        public static class FieldNames
        {
            public const string Tuning = nameof(_tuning);
            public const string Mode = nameof(_mode);
            public const string PickModeAtStart = nameof(_pickModeAtStart);
            public const string SixBodies = nameof(_sixBodies);
            public const string TenBodies = nameof(_tenBodies);
            public const string ArenaRoot = nameof(_arenaRoot);
            public const string Square = nameof(_square);
            public const string HudPanelSettings = nameof(_hudPanelSettings);
            public const string Camera = nameof(_camera);
            public const string CameraDirector = nameof(_cameraDirector);
        }
    }
}
