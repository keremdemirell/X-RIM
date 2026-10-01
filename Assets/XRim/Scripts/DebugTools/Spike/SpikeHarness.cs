using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using XRim.Config;
using XRim.Core;
using XRim.Presentation.Playback;
using XRim.Rules;
using XRim.Rules.Arena;
using XRim.Rules.Match;
using XRim.Rules.Paths;
using XRim.Rules.Settings;
using XRim.Simulation;
using XRim.Simulation.Drivers;
using XRim.Simulation.Execution;
using XRim.Simulation.Physics;
using XRim.Simulation.Settings;
using XRim.Simulation.Unity2D;
using Debug = UnityEngine.Debug;

namespace XRim.DebugTools.Spike
{
    /// <summary>
    /// The Session 02 feel spike: one dummy with a rapier or a mace, and a passive target dummy. Draw a torso-relative path
    /// with the mouse; Space simulates the whole swing up front in the hidden physics scene and plays the recording back
    /// at the chosen speed (ARCHITECTURE §1). Weapon, driver (D1) and segmentation (D2) can be switched live; every other
    /// knob is in the Debug overlay's Tuning tab and applies from the next swing.
    /// Development tool only: it lives in XRim.DebugTools, and the spike scene is never added to the build.
    /// </summary>
    public sealed class SpikeHarness : MonoBehaviour
    {
        private const int DefaultSpeedIndex = 4;
        private const float MinTargetDistanceUnits = 100f;
        private const float FloorThicknessUnits = 100f;
        private const int FloorSortingOrder = -100;
        private const string SpriteShaderName = "Universal Render Pipeline/2D/Sprite-Unlit-Default";
        private const string FallbackShaderName = "Sprites/Default";

        private const string NewLine = "\n";
        private const float HudMargin = 8f;
        private const float HudPadding = 6f;
        private const float HudWidth = 640f;
        private const float HudLineHeight = 18f;
        private const int HudLineCount = 7;
        private const float ContactLogHeight = 220f;
        private const string ReportFolderName = "Logs";
        private const string ReportFileName = "XRimSpikeReport.txt";

        /// <summary>Screen corner of the Debug overlay's button (GUI coordinates); strokes never start there.</summary>
        private static readonly Rect OverlayButtonArea = new Rect(0f, 0f, 110f, 60f);

        private static readonly float[] PlaybackSpeeds = { 0.05f, 0.1f, 0.25f, 0.5f, 1f, 2f };
        private static readonly WeaponId[] Weapons = { WeaponIds.Rapier, WeaponIds.Mace };
        private static readonly Color FloorColor = new Color(0.22f, 0.22f, 0.25f);

        [SerializeField] private TuningProfile _tuning;
        [SerializeField] private Camera _camera;
        [SerializeField] private Ragdoll _sixBodies;
        [SerializeField] private Ragdoll _tenBodies;
        [SerializeField] private Sprite _square;

        [Tooltip("Where the target stands when the weapon changes: its chest at this share of the weapon's reach " +
                 "(arm + weapon). Spike only; matches start at ArenaSettings.StartingGapUnits.")]
        [SerializeField] private float _targetReachFraction = 0.85f;

        [SerializeField] private float _targetDistanceStepUnits = 25f;

        private readonly IWeaponAimModel _aim = new AimFromShoulderModel();
        private readonly PathBuilder _pathBuilder = new PathBuilder(new RulePolicies());
        private readonly TimelinePlayer _player = new TimelinePlayer();
        private readonly List<string> _issues = new List<string>();
        private Unity2DPhysicsWorld _world;
        private ArenaSpace _space;
        private SpikePathDrawer _drawer;
        private SpikeVisualDummy _attacker;
        private SpikeVisualDummy _target;
        private RagdollSegmentation _visualSegmentation;
        private PoseSnapshot _board;
        private MatchState _state;
        private BuiltPath _built;
        private SwingResult _lastResult;
        private double _lastSimulateMilliseconds;
        private float _targetDistanceUnits;
        private bool _weaponStartsInsideTarget;
        private int _weaponIndex;
        private int _speedIndex = DefaultSpeedIndex;
        private bool _playing;
        private string _lastIssues = string.Empty;
        private Rect _hudRect;
        private Rect _contactLogRect;
        private readonly SpikeContactLog _contactLog = new SpikeContactLog();
        private MatchState _lastResultState;
        private string _diagnosticsMessage = string.Empty;

        public TuningProfile Tuning => _tuning;

        /// <summary>The last simulated swing (contacts, timeline), or null.</summary>
        public SwingResult LastResult => _lastResult;

        private WeaponId CurrentWeapon => Weapons[_weaponIndex];
        private BodyPose AttackerTorso => _board.Left.Get(BodyPart.Torso);

        private void Start()
        {
            string missing = (_tuning == null ? " tuning profile" : string.Empty) + (_camera == null ? " camera" : string.Empty) +
                             (_sixBodies == null ? " 6-body dummy" : string.Empty) + (_tenBodies == null ? " 10-body dummy" : string.Empty);
            if (missing.Length > 0)
            {
                Debug.LogError($"[XRim] The spike harness is missing:{missing}. Run XRim/Spike/Create Spike Scene again.", this);
                enabled = false;
                return;
            }

            // Gameplay physics only moves when the simulation steps its own hidden scene.
            Physics2D.simulationMode = SimulationMode2D.Script;
            _space = _tuning.ArenaSpace;
            _world = new Unity2DPhysicsWorld(_space, new RagdollPrefabSet(_sixBodies, _tenBodies));
            _player.PoseChanged += ShowPose;
            _drawer = new SpikePathDrawer(transform, CreateLineMaterial());
            Snapshot(out RulesSettings rules, out _);
            CreateFloor(rules.Arena.WidthUnits);
            PlaceTargetInsideReach();
            ResetBoard();
        }

        private void OnDestroy()
        {
            _player.PoseChanged -= ShowPose;
            if (_world != null) _world.Dispose();
        }

        private void Update()
        {
            HandleKeys();
            if (_playing)
            {
                _player.Advance(Time.unscaledDeltaTime);
                if (_player.IsFinished && !_player.IsPaused) FinishPlayback();
                return;
            }

            HandleMouse();
        }

        private void HandleKeys()
        {
            Keyboard keyboard = Keyboard.current;
            // Never steal keys while a tuning text field is being edited.
            if (keyboard == null || GUIUtility.keyboardControl != 0) return;

            if (keyboard.spaceKey.wasPressedThisFrame) Execute();
            if (keyboard.rKey.wasPressedThisFrame) ResetBoard();
            if (keyboard.cKey.wasPressedThisFrame) ClearPath();
            if (keyboard.digit1Key.wasPressedThisFrame) SelectWeapon(0);
            if (keyboard.digit2Key.wasPressedThisFrame) SelectWeapon(1);
            if (keyboard.dKey.wasPressedThisFrame) ToggleDriver();
            if (keyboard.bKey.wasPressedThisFrame) ToggleSegmentation();
            if (keyboard.leftBracketKey.wasPressedThisFrame) ChangeTargetDistance(-_targetDistanceStepUnits);
            if (keyboard.rightBracketKey.wasPressedThisFrame) ChangeTargetDistance(_targetDistanceStepUnits);
            if (keyboard.minusKey.wasPressedThisFrame) ChangeSpeed(-1);
            if (keyboard.equalsKey.wasPressedThisFrame) ChangeSpeed(1);
            if (keyboard.qKey.wasPressedThisFrame) Replay();
            if (keyboard.f9Key.wasPressedThisFrame) RunDiagnostics();
            if (!_playing) return;
            if (keyboard.pKey.wasPressedThisFrame) _player.IsPaused = !_player.IsPaused;
            if (keyboard.leftArrowKey.wasPressedThisFrame) _player.StepFrames(-1);
            if (keyboard.rightArrowKey.wasPressedThisFrame) _player.StepFrames(1);
        }

        private void HandleMouse()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null) return;
            Vector2 screen = mouse.position.ReadValue();
            var guiPoint = new Vector2(screen.x, Screen.height - screen.y);
            bool startAllowed = !OverlayButtonArea.Contains(guiPoint) && !_hudRect.Contains(guiPoint) &&
                                !_contactLogRect.Contains(guiPoint) && GUIUtility.hotControl == 0;

            // Clicking into the arena takes keyboard focus away from any tuning text field, so the hotkeys work again.
            if (startAllowed && mouse.leftButton.wasPressedThisFrame) GUIUtility.keyboardControl = 0;
            Vector3 world = _camera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -_camera.transform.position.z));
            if (_drawer.Track(mouse.leftButton.isPressed, startAllowed, world, AttackerTorso, _space))
            {
                Snapshot(out RulesSettings rules, out _);
                RebuildPath(rules);
            }
        }

        /// <summary>Back to the guard stance at the current distance, with the current weapon and segmentation.</summary>
        private void ResetBoard()
        {
            Snapshot(out RulesSettings rules, out SimulationSettings simulation);
            _playing = false;
            EnsureVisuals(simulation.Segmentation);

            WeaponStats weapon = rules.FindWeapon(CurrentWeapon);
            _board = SpikeBoard.CreatePose(Template(simulation.Segmentation), simulation, _space, _aim, weapon, _targetDistanceUnits);
            _state = SpikeBoard.CreateState(rules, CurrentWeapon);
            _weaponStartsInsideTarget = SpikeBoard.WeaponStartsInsideTarget(_board, weapon, simulation.Ragdoll);

            _attacker.Hold(weapon, _space);
            _target.Hold(null, _space);
            ShowPose(_board);
            RebuildPath(rules);
        }

        /// <summary>Simulates the whole swing up front with the current tuning, then plays it back.</summary>
        private void Execute()
        {
            Snapshot(out RulesSettings rules, out SimulationSettings simulation);
            if (simulation.Segmentation != _visualSegmentation)
            {
                ResetBoard();
                Snapshot(out rules, out simulation);
            }

            RebuildPath(rules);
            WeaponPath path = _built != null ? _built.Path : WeaponPath.Empty;
            var input = new SwingInput(_board, _state, new PerSide<WeaponPath>(path, null), rules, simulation);
            Stopwatch watch = Stopwatch.StartNew();
            _lastResult = new SwingSimulator(_world, _aim).Run(input);
            _lastResultState = _state;
            watch.Stop();
            _lastSimulateMilliseconds = watch.Elapsed.TotalMilliseconds;
            Play();
        }

        private void Replay()
        {
            if (_lastResult != null) Play();
        }

        private void Play()
        {
            _player.PlaybackSpeed = PlaybackSpeeds[_speedIndex];
            _player.Play(_lastResult.Timeline);
            _playing = true;
        }

        /// <summary>The dummies freeze in their new poses (GDD §3 stance persistence); the next swing starts from there.</summary>
        private void FinishPlayback()
        {
            _playing = false;
            _board = _lastResult.FinalPose;
            Snapshot(out RulesSettings rules, out _);
            RebuildPath(rules);
        }

        private void ClearPath()
        {
            _drawer.Clear();
            _built = null;
        }

        /// <summary>GDD §6 (Decided): switching weapons erases the drawn path.</summary>
        private void SelectWeapon(int index)
        {
            if (index == _weaponIndex) return;
            _weaponIndex = index;
            ClearPath();
            PlaceTargetInsideReach();
            ResetBoard();
        }

        private void ToggleDriver()
        {
            SimulationConfig config = _tuning.SimulationConfig;
            if (config == null) return;
            config.Settings.WeaponDriver = config.Settings.WeaponDriver == WeaponDriverKind.Kinematic
                ? WeaponDriverKind.Motor
                : WeaponDriverKind.Kinematic;
            DebugAssets.MarkDirty(config);
        }

        private void ToggleSegmentation()
        {
            SimulationConfig config = _tuning.SimulationConfig;
            if (config == null) return;
            config.Settings.Segmentation = config.Settings.Segmentation == RagdollSegmentation.SixBodies
                ? RagdollSegmentation.TenBodies
                : RagdollSegmentation.SixBodies;
            DebugAssets.MarkDirty(config);
            ResetBoard();
        }

        /// <summary>Puts the target just inside the current weapon's reach, so it can be hit without starting inside it.</summary>
        private void PlaceTargetInsideReach()
        {
            Snapshot(out RulesSettings rules, out SimulationSettings simulation);
            WeaponStats weapon = rules.FindWeapon(CurrentWeapon);
            if (weapon != null) _targetDistanceUnits = SpikeBoard.DistanceInsideReach(rules.Paths, simulation.Ragdoll, weapon, _targetReachFraction);
        }

        private void ChangeTargetDistance(float deltaUnits)
        {
            _targetDistanceUnits = Mathf.Max(MinTargetDistanceUnits, _targetDistanceUnits + deltaUnits);
            ResetBoard();
        }

        private void ChangeSpeed(int delta)
        {
            _speedIndex = Mathf.Clamp(_speedIndex + delta, 0, PlaybackSpeeds.Length - 1);
            _player.PlaybackSpeed = PlaybackSpeeds[_speedIndex];
        }

        /// <summary>Applies the path rules (lead-in from the weapon tip, resample, reach clamp, ink cut-off) to the stroke.</summary>
        private void RebuildPath(RulesSettings rules)
        {
            WeaponStats weapon = rules.FindWeapon(CurrentWeapon);
            if (!_drawer.HasStroke || weapon == null || !_board.Left.HasHeldItem)
            {
                _built = null;
                _drawer.ShowPath(null, AttackerTorso, _space, 0f);
                return;
            }

            BodyPose grip = _board.Left.HeldItem;
            Vec2 tip = grip.PositionUnits + Vec2.FromAngleDegrees(grip.RotationDegrees) * weapon.LengthUnits;
            _built = _pathBuilder.Build(WeaponPath.Empty, _drawer.Stroke, TorsoFrame.ToLocal(tip, AttackerTorso, Side.Left), weapon,
                rules.Paths);
            _drawer.ShowStroke(AttackerTorso, _space);
            _drawer.ShowPath(_built.Path, AttackerTorso, _space, weapon.InkThicknessUnits);
        }

        private void ShowPose(PoseSnapshot pose)
        {
            _attacker.View.ApplyPose(pose.Left, _space);
            _target.View.ApplyPose(pose.Right, _space);
        }

        private void EnsureVisuals(RagdollSegmentation segmentation)
        {
            if (_attacker != null && segmentation == _visualSegmentation) return;
            _attacker?.Destroy();
            _target?.Destroy();
            Ragdoll template = Template(segmentation);
            _attacker = SpikeVisualDummy.Create(template, transform, "AttackerView");
            _target = SpikeVisualDummy.Create(template, transform, "TargetView");
            _visualSegmentation = segmentation;
        }

        private Ragdoll Template(RagdollSegmentation segmentation) =>
            segmentation == RagdollSegmentation.TenBodies ? _tenBodies : _sixBodies;

        /// <summary>Settings snapshot for one swing (live tuning applies from the next swing). New issues are logged once.</summary>
        private void Snapshot(out RulesSettings rules, out SimulationSettings simulation)
        {
            _issues.Clear();
            rules = _tuning.BuildRulesSettings(_issues);
            simulation = _tuning.BuildSimulationSettings(_issues);
            rules.Validate(_issues);
            simulation.Validate(_issues);
            string text = string.Join("\n", _issues);
            if (text.Length > 0 && text != _lastIssues) Debug.LogWarning("[XRim] Spike tuning issues:\n" + text, _tuning);
            _lastIssues = text;
        }

        private void CreateFloor(float widthUnits)
        {
            if (_square == null) return;
            var floor = new GameObject("FloorView");
            floor.transform.SetParent(transform, false);
            float thickness = _space.ToWorldLength(FloorThicknessUnits);
            floor.transform.position = new Vector3(0f, -thickness * 0.5f, 0f);
            floor.transform.localScale = new Vector3(_space.ToWorldLength(widthUnits), thickness, 1f);
            var renderer = floor.AddComponent<SpriteRenderer>();
            renderer.sprite = _square;
            renderer.color = FloorColor;
            renderer.sortingOrder = FloorSortingOrder;
        }

        private static Material CreateLineMaterial()
        {
            Shader shader = Shader.Find(SpriteShaderName);
            if (shader == null) shader = Shader.Find(FallbackShaderName);
            return shader != null ? new Material(shader) : null;
        }

        private void OnGUI()
        {
            if (_board == null) return;
            float height = HudPadding * 2f + HudLineHeight * HudLineCount;
            _hudRect = new Rect(Screen.width - HudWidth - HudMargin, HudMargin, HudWidth, height);
            GUI.Box(_hudRect, GUIContent.none);
            GUI.Label(new Rect(_hudRect.x + HudPadding, _hudRect.y + HudPadding, HudWidth - HudPadding * 2f, height - HudPadding * 2f),
                HudText());

            _contactLogRect = new Rect(_hudRect.x, _hudRect.yMax + HudMargin, HudWidth, ContactLogHeight);
            _contactLog.Draw(_contactLogRect, _lastResult, _lastResultState, _player.CurrentTime, _playing);
        }

        /// <summary>F9: measures the PT1 evidence with the live tuning and saves the report (Logs/XRimSpikeReport.txt in the Editor).</summary>
        private void RunDiagnostics()
        {
            Snapshot(out RulesSettings rules, out SimulationSettings simulation);
            var diagnostics = new SpikeDiagnostics(_world, _sixBodies, _tenBodies, _space, _aim);
            string report = diagnostics.Run(rules, simulation, _targetReachFraction);
#if UNITY_EDITOR
            string folder = Path.Combine(Application.dataPath, "..", ReportFolderName);
#else
            string folder = Application.persistentDataPath;
#endif
            Directory.CreateDirectory(folder);
            string file = Path.GetFullPath(Path.Combine(folder, ReportFileName));
            File.WriteAllText(file, report);
            _diagnosticsMessage = "Diagnostics saved to " + file;
            Debug.Log($"[XRim] {_diagnosticsMessage}\n{report}");
            ResetBoard();
        }

        private string HudText()
        {
            SimulationSettings live = _tuning.SimulationConfig != null ? _tuning.SimulationConfig.Settings : null;
            string driver = live != null ? live.WeaponDriver.ToString() : "?";
            string bodies = live != null ? ((int)live.Segmentation).ToString() : "?";
            string weapon = CurrentWeapon.Value;
            string speed = PlaybackSpeeds[_speedIndex] + "×" + (_playing && _player.IsPaused ? " (paused)" : string.Empty);
            return "X-RIM feel spike (Session 02)\n" +
                   "Draw: drag with the left mouse button    Swing: Space    Reset stance: R    Clear path: C\n" +
                   $"Weapon: {weapon} [1/2]    Driver: {driver} [D]    Bodies: {bodies} [B]    Target distance: {_targetDistanceUnits:0} [ [ ] ]\n" +
                   $"Playback: {speed} [- =]    Pause [P]    Frame step [← →]    Replay [Q]    Diagnostics report [F9]\n" +
                   PathLine() + NewLine + SwingLine() + NewLine +
                   (_weaponStartsInsideTarget ? "WARNING: the weapon starts inside the target. Press ] to step back." : _diagnosticsMessage);
        }

        private string PathLine()
        {
            if (_built == null) return "Path: none (drag to draw; the weapon tip is the start, D3)";
            InkedPath inked = _built.Inked;
            string notes = (_built.WasClampedByReach ? ", clamped to reach" : string.Empty) +
                           (inked.WasCut ? ", cut where the ink ran out" : string.Empty) +
                           (_built.WasCutAtBreak ? ", cut at a too-sharp turn" : string.Empty);
            return $"Path: {inked.Path.LengthUnits:0} units, ink {inked.Ink.CostUnits:0} of {inked.BudgetUnits:0}{notes}";
        }

        private string SwingLine()
        {
            if (_lastResult == null) return "Last swing: none yet";
            double seconds = _lastResult.Timeline.Duration.Seconds;
            return $"Last swing: {_lastResult.StepsSimulated} steps ({seconds:0.000} s), {_lastResult.EndReason}, " +
                   $"simulated in {_lastSimulateMilliseconds:0.0} ms, {_lastResult.Contacts.Count} contacts";
        }

        /// <summary>Serialized field names, for the Editor tool that builds the spike scene.</summary>
        public static class FieldNames
        {
            public const string Tuning = nameof(_tuning);
            public const string Camera = nameof(_camera);
            public const string SixBodies = nameof(_sixBodies);
            public const string TenBodies = nameof(_tenBodies);
            public const string Square = nameof(_square);
        }
    }
}
