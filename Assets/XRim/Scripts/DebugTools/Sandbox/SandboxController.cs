using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using XRim.App;
using XRim.Config;
using XRim.Core;
using XRim.DebugTools.Diagnostics;
using XRim.Input;
using XRim.Networking;
using XRim.Presentation.Arena;
using XRim.Presentation.Cameras;
using XRim.Presentation.UI;
using XRim.Rules;
using XRim.Rules.Match;
using XRim.Rules.Paths;
using XRim.Rules.Planning;
using XRim.Rules.Settings;
using XRim.Simulation;
using XRim.Simulation.Drivers;
using XRim.Simulation.Physics;
using XRim.Simulation.Recording;
using XRim.Simulation.Unity2D;

namespace XRim.DebugTools.Sandbox
{
    /// <summary>
    /// The debug sandbox (MatchBootstrap in <see cref="MatchMode.Sandbox"/>): plan both sides on one screen, hot-seat style.
    /// One side at a time is played with the real touch scheme (<see cref="TouchPlanSource"/>, GDD §4–6): drag in the arena to
    /// draw its path, flick in the body zone at the screen edge to set its body move; the mouse acts as a finger. Tab (or the
    /// buttons) switches the side being played, and the view flips as that player would see it. The mirrored (left-handed)
    /// layout is a dev toggle (D24). Everything reaches the rules as real planning commands, so they apply exactly as in a
    /// match. The planning HUD (<see cref="PlanningHud"/>, weapon selector, Ready, timer, ink, opponent) serves the side being
    /// played; the sandbox's own panel keeps the debug extras: both sides' weapons and moves, Execute (both Ready), keys. The
    /// planning timer is frozen unless the panel's checkbox runs it (to try the weapon lock-out).
    /// </summary>
    internal sealed class SandboxController : MonoBehaviour
    {
        private const float HudMargin = 8f;
        private const float HudWidth = 560f;
        private const float HudHeight = 410f;

        /// <summary>The panel sits below the planning HUD's top row (selector, ink, opponent), as a share of the screen height.</summary>
        private const float HudTopScreenFraction = 0.16f;
        private const float MoveButtonWidth = 72f;

        /// <summary>The body moves in the order the HUD offers them.</summary>
        private static readonly BodyMove[] Moves = { BodyMove.None, BodyMove.Crouch, BodyMove.Lunge, BodyMove.StepBack, BodyMove.Jump };

        private static readonly Side[] Sides = { Side.Left, Side.Right };
        private const string ReportFolderName = "Logs";
        private const string ReportFileName = "XRimFeelReport.txt";

        private readonly PerSide<PathPreview> _preview = new PerSide<PathPreview>(null, null);
        private readonly SandboxReadout _readout = new SandboxReadout();
        private readonly IHandednessLayoutPolicy _handednessPolicy = new MirrorWhenLeftHanded();
        private MatchBootstrap _bootstrap;
        private DebugOverlay _overlay;
        private Camera _camera;
        private FixedSideCameraDirector _director;
        private TouchInputReader _reader;
        private InputConfig _input;
        private bool _ownsInput;
        private PathPreviewBuilder _previewBuilder;
        private PerSide<TouchPlanSource> _sources;
        private PlanningHud _hud;
        private PlanningHudPresenter _presenter;
        private HudTouchBlocker _hudBlocker;
        private PerSide<PathPreviewView> _views;
        private Side _activeSide = Side.Left;
        private Handedness _handedness;
        private Rect _hudRect;
        private string _diagnosticsMessage = string.Empty;

        public void Init(MatchBootstrap bootstrap, DebugOverlay overlay)
        {
            _bootstrap = bootstrap;
            _overlay = overlay;
        }

        private void Update()
        {
            if (_bootstrap == null) return;
            if (_sources == null)
            {
                TryStart();
                return;
            }

            HandleKeys();
            _sources[_activeSide].Tick();
            UpdatePreviews();
            ShowHud();
        }

        private void OnDestroy()
        {
            if (_views != null)
            {
                _views.Left.Destroy();
                _views.Right.Destroy();
            }

            _presenter?.Dispose();
            if (_hud != null) Destroy(_hud.gameObject);
            if (_ownsInput && _input != null) Destroy(_input);
        }

        /// <summary>Waits until the match scene has composed, then hands it the two touch plan sources.</summary>
        private void TryStart()
        {
            if (!_bootstrap.IsComposed || _bootstrap.HasStarted) return;

            _camera = Camera.main;
            if (_camera == null)
            {
                Debug.LogError("[XRim] The sandbox needs a camera tagged MainCamera.", this);
                enabled = false;
                return;
            }

            _input = _bootstrap.Tuning.InputConfig;
            if (_input == null)
            {
                Debug.LogWarning("[XRim] The tuning profile has no Input asset: the sandbox uses the code defaults.", this);
                _input = ScriptableObject.CreateInstance<InputConfig>();
                _ownsInput = true;
            }

            _handedness = _input.DevHandedness;
            _director = _camera.GetComponent<FixedSideCameraDirector>();
            _reader = _bootstrap.GetComponent<TouchInputReader>();
            if (_reader == null) _reader = _bootstrap.gameObject.AddComponent<TouchInputReader>();
            _reader.MouseActsAsTouch = true; // the mouse is a finger from here on (Mouse.current reads nothing)

            _hud = PlanningHud.Create(_input.ReferenceResolution);
            _hudBlocker = new HudTouchBlocker(_hud);
            _presenter = new PlanningHudPresenter(_hud, _bootstrap.Authority, _bootstrap.Setup, _bootstrap.RulesSettings.Match);

            // The swipe and tap numbers are read once, here: edit them in the Input asset before pressing Play.
            SwipeSettings swipe = SwipeSettings.From(_input);
            DualZoneSettings zones = DualZoneSettings.From(_input);
            var mapper = new ScreenToArenaMapper();
            var blocker = new UiBlocker(this);
            _sources = PerSide<TouchPlanSource>.Create(side => new TouchPlanSource(side,
                new DualZoneInputScheme(_reader, new FourWaySwipeClassifier(swipe), swipe, zones, mapper, ReadView, blocker), LayoutFor(side)));

            _previewBuilder = new PathPreviewBuilder(_bootstrap.Policies);
            Material material = ArenaLines.CreateMaterial();
            _views = PerSide<PathPreviewView>.Create(side => new PathPreviewView(_bootstrap.ArenaRoot, material, side.ToString()));
            _bootstrap.StartMatch(new IPlanSource[] { _sources.Left, _sources.Right });
            ApplyActiveSide();
        }

        /// <summary>What the camera shows now, so touches map to the arena correctly even if the camera moves.</summary>
        private ArenaViewFrame ReadView() => CameraArenaView.Read(_camera, _bootstrap.Space);

        private ScreenLayout LayoutFor(Side side) => new ScreenLayout(Screen.width, Screen.height, _input.BodyZoneWidthFraction,
            _handednessPolicy.IsScreenMirrored(_handedness), side);

        /// <summary>Only the side being played reads touches, and the view is flipped as that player would see it (GDD §4).</summary>
        private void ApplyActiveSide()
        {
            foreach (Side side in Sides)
            {
                _sources[side].IsActive = side == _activeSide;
            }

            if (_director != null) _director.SetArenaFlipped(_sources[_activeSide].Layout.ArenaViewFlipped);
        }

        private void SetHandedness(Handedness handedness)
        {
            _handedness = handedness;
            foreach (Side side in Sides)
            {
                _sources[side].Layout = LayoutFor(side);
            }

            ApplyActiveSide();
        }

        private bool IsPlanning => _sources.Left.IsPlanning && _sources.Right.IsPlanning;

        private void HandleKeys()
        {
            Keyboard keyboard = Keyboard.current;
            // Never steal keys while a tuning text field is being edited.
            if (keyboard == null || GUIUtility.keyboardControl != 0 || !IsPlanning) return;

            if (keyboard.f9Key.wasPressedThisFrame) RunDiagnostics();
            if (keyboard.tabKey.wasPressedThisFrame) SwitchSide();
            if (keyboard.spaceKey.wasPressedThisFrame) Execute();
            TouchPlanSource active = _sources[_activeSide];
            if (keyboard.cKey.wasPressedThisFrame) active.Send(new ClearPathCommand());
            IReadOnlyList<WeaponId> loadout = _bootstrap.Setup.Fighters[_activeSide].Loadout;
            if (keyboard.digit1Key.wasPressedThisFrame && loadout.Count > 0) active.Send(new SelectWeaponCommand(loadout[0]));
            if (keyboard.digit2Key.wasPressedThisFrame && loadout.Count > 1) active.Send(new SelectWeaponCommand(loadout[1]));
            if (keyboard.digit3Key.wasPressedThisFrame && loadout.Count > 2) active.Send(new SelectWeaponCommand(loadout[2]));

            // Body moves from the keys, as the swipes are (§5): down, up, toward or away from the opponent; N clears it.
            // Like the swipes, the arrows follow the screen: → is toward the opponent when this player sees their dummy facing right
            // (the view is flipped for the right side, mirrored when left-handed), whichever arena side it is.
            ScreenLayout layout = active.Layout;
            if (keyboard.downArrowKey.wasPressedThisFrame) SetBodyMove(active, BodyMove.Crouch);
            if (keyboard.upArrowKey.wasPressedThisFrame) SetBodyMove(active, BodyMove.Jump);
            if (keyboard.rightArrowKey.wasPressedThisFrame) SetBodyMove(active, layout.HorizontalMove(1f) ?? BodyMove.None);
            if (keyboard.leftArrowKey.wasPressedThisFrame) SetBodyMove(active, layout.HorizontalMove(-1f) ?? BodyMove.None);
            if (keyboard.nKey.wasPressedThisFrame) SetBodyMove(active, BodyMove.None);
        }

        private static void SetBodyMove(TouchPlanSource source, BodyMove move) => source.Send(new SetBodyMoveCommand(move));

        /// <summary>
        /// True when a screen point (pixels, y up) is over the sandbox panel, the fight readout or the debug overlay, so a
        /// touch there is a button press and not a stroke.
        /// </summary>
        private bool IsOverUi(Vec2 screen)
        {
            var guiPoint = new Vector2(screen.X, Screen.height - screen.Y);
            return GUIUtility.hotControl != 0 || _hudRect.Contains(guiPoint) || _readout.Area.Contains(guiPoint) ||
                   (_overlay != null && _overlay.Covers(guiPoint));
        }

        /// <summary>True when a screen point (pixels, y up) is on one of the planning HUD's controls.</summary>
        private bool IsOverHud(Vec2 screen) => _hudBlocker != null && _hudBlocker.Blocks(screen);

        /// <summary>The HUD serves the side being played: its timer, selector, Ready, ink, body move and the other side's public state.</summary>
        private void ShowHud()
        {
            _presenter.Source = _sources[_activeSide];
            _presenter.Tick(_bootstrap.AuthorityNowSeconds, _preview[_activeSide]);
        }

        private BodyPose Root(Side side) => TurnStartRoot.Of(_sources[side].Window.Board.Pose.Get(side));

        private void SwitchSide()
        {
            _activeSide = _activeSide.Opponent();
            ApplyActiveSide();
        }

        private void Execute()
        {
            foreach (Side side in Sides)
            {
                _sources[side].Send(new SetReadyCommand(true));
            }
        }

        /// <summary>
        /// F9: measures the feel report with the live tuning in a separate physics world (the match is untouched) and saves it
        /// (Logs/XRimFeelReport.txt in the Editor, the app's data folder on a device).
        /// </summary>
        private void RunDiagnostics()
        {
            var issues = new List<string>();
            RulesSettings rules = _bootstrap.Tuning.BuildRulesSettings(issues);
            SimulationSettings simulation = _bootstrap.Tuning.BuildSimulationSettings(issues);
            string report;
            using (var world = new Unity2DPhysicsWorld(_bootstrap.Space, _bootstrap.RagdollPrefabs))
            {
                report = new FeelDiagnostics(world).Run(rules, simulation);
            }

#if UNITY_EDITOR
            string folder = Path.Combine(Application.dataPath, "..", ReportFolderName);
#else
            string folder = Application.persistentDataPath;
#endif
            Directory.CreateDirectory(folder);
            string file = Path.GetFullPath(Path.Combine(folder, ReportFileName));
            File.WriteAllText(file, report);
            _diagnosticsMessage = "Feel report saved to " + file;
            Debug.Log($"[XRim] {_diagnosticsMessage}\n{report}");
        }

        /// <summary>
        /// Builds each side's path as the rules will (the stroke being drawn, or the last one set; the weapon tip and ink from the
        /// window) and draws it: the executed path at the weapon's width, clipped at reach and where the ink runs out.
        /// </summary>
        private void UpdatePreviews()
        {
            foreach (Side side in Sides)
            {
                TouchPlanSource source = _sources[side];
                WeaponStats weapon = source.IsPlanning ? Weapon(source) : null;
                if (weapon == null)
                {
                    _preview[side] = null;
                    _views[side].Clear();
                    continue;
                }

                bool drawing = source.ActiveStroke.Count > 0;
                IReadOnlyList<Vec2> stroke = drawing ? source.ActiveStroke : source.Draft.Stroke.Points;
                Vec2 tip = source.Window.WeaponTips[side].TipLocal(source.Draft.Weapon);
                PathPreview preview = _previewBuilder.Build(stroke, tip, weapon, _bootstrap.RulesSettings.Paths);
                _preview[side] = preview;
                _views[side].Show(preview, Root(side), side, _bootstrap.Space, MoveFrame(source), drawing);
            }
        }

        /// <summary>
        /// Where the side's path frame will be once its body move is at full extent (a hop: at its top), as the simulation plays
        /// it; null without a body move.
        /// </summary>
        private BodyPose? MoveFrame(TouchPlanSource source)
        {
            BodyMove move = source.Draft.BodyMove;
            BodyMoveStats stats = move != BodyMove.None ? LiveBodyMove(move) : null;
            if (stats == null) return null;

            Side side = source.Side;
            BoardSnapshot board = source.Window.Board;
            var start = BodyMoveStart.Of(side, board.Pose.Get(side), board.State.Fighters[side].Handedness, _bootstrap.SimulationSettings.Ragdoll);
            var driver = new StanceBodyMoveDriver();
            driver.Begin(stats, start);
            bool isHop = stats.DisplacementUnits.Y > 0f && !stats.IsLeanInPlace;
            BodyPose root = driver.Evaluate(SimTime.FromSeconds(isHop ? stats.DurationSeconds * 0.5 : stats.DurationSeconds)).Root;
            return _bootstrap.RulesSettings.Paths.PathTiltsWithTorsoLean ? root : new BodyPose(root.PositionUnits, start.Root.RotationDegrees);
        }

        /// <summary>A body move's data as the tuning panel has it now: the sandbox plays body moves with the live tuning.</summary>
        private BodyMoveStats LiveBodyMove(BodyMove move)
        {
            foreach (SettingsConfigBase config in _bootstrap.Tuning.SettingsConfigs())
            {
                if (config is BodyMoveDefinition definition && definition.Settings.Move == move) return definition.Settings;
            }

            return _bootstrap.RulesSettings.FindBodyMove(move);
        }

        private WeaponStats Weapon(TouchPlanSource source)
        {
            WeaponStats stats = _bootstrap.RulesSettings.FindWeapon(source.Draft.Weapon);
            return stats?.WithInkLengthMultiplier(source.Window.Constraints[source.Side].InkLengthMultiplier);
        }

        private void OnGUI()
        {
            if (_sources == null) return;
            _hudRect = new Rect(Screen.width - HudWidth - HudMargin, Screen.height * HudTopScreenFraction, HudWidth, HudHeight);
            GUILayout.BeginArea(_hudRect, GUI.skin.box);
            if (IsPlanning) DrawPlanning();
            else GUILayout.Label(MatchOverLine() ?? "Executing and playing back… (Debug → Playback: slow motion, loop, re-simulate, hits)");
            GUILayout.EndArea();
            _readout.Draw(_bootstrap, _sources, _sources[_activeSide].Layout.ArenaViewFlipped);
        }

        /// <summary>Null while the match goes on. A KO ends it (Session 06); to fight again, stop and enter Play mode.</summary>
        private string MatchOverLine()
        {
            MatchOutcome outcome = _bootstrap.Flow != null ? _bootstrap.Flow.Outcome : null;
            return outcome == null
                ? null
                : $"Match over after {outcome.TurnCount} turn(s): {outcome.Winner} wins by {outcome.Reason}. Stop and press Play to fight again.";
        }

        private void DrawPlanning()
        {
            string timer = _bootstrap.IsPlanningTimerFrozen ? "timer frozen" : "timer running";
            GUILayout.Label($"X-RIM sandbox: turn {_sources.Left.Window.TurnIndex + 1}, planning ({timer})");
            GUILayout.BeginHorizontal();
            GUILayout.Label("Playing:");
            foreach (Side side in Sides)
            {
                string label = side == _activeSide ? $"[ {side.ToString().ToUpperInvariant()} ]" : side.ToString();
                if (GUILayout.Button(label) && side != _activeSide) SwitchSide();
            }

            GUILayout.Label("(Tab: the view flips as that player sees it)");
            GUILayout.EndHorizontal();

            bool leftHanded = GUILayout.Toggle(_handedness == Handedness.Left, "Left-handed: mirror the layout (dev setting, D24)");
            if (leftHanded != (_handedness == Handedness.Left)) SetHandedness(leftHanded ? Handedness.Left : Handedness.Right);

            bool runTimer = GUILayout.Toggle(!_bootstrap.IsPlanningTimerFrozen, "Run the planning timer (to try the weapon lock-out)");
            _bootstrap.IsPlanningTimerFrozen = !runTimer;

            foreach (Side side in Sides)
            {
                DrawSide(side);
            }

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Clear path (C)")) _sources[_activeSide].Send(new ClearPathCommand());
            if (GUILayout.Button("Execute (Space)")) Execute();
            GUILayout.EndHorizontal();
            GUILayout.Label("The mouse is a finger. Drag in the arena to draw the active side's path; a click is a tap (the item moves " +
                            "there and holds, e.g. a raised shield). Flick in the body zone (the strip at the screen edge over your own " +
                            "dummy): up jump, down crouch, toward the opponent lunge, away step back; a quick click there clears the move. " +
                            "Keys: weapons 1/2/3, arrows (↓ crouch, ↑ jump, →/← lunge or step back), N none, C clear path. Feel report: F9. " +
                            "Simulation and body-move tuning apply from the next Execute; other rules tuning from the next match. " +
                            "HP, limb damage, stuns and staggers: bottom of the screen. Hits, clashes and blocks: Debug → Playback.");
            if (_diagnosticsMessage.Length > 0) GUILayout.Label(_diagnosticsMessage);
        }

        private void DrawSide(Side side)
        {
            TouchPlanSource source = _sources[side];
            GUILayout.BeginHorizontal();
            GUILayout.Label(side + ":", GUILayout.Width(44f));
            foreach (WeaponId weapon in _bootstrap.Setup.Fighters[side].Loadout)
            {
                string label = weapon == source.Draft.Weapon ? $"[{weapon.Value}]" : weapon.Value;
                if (GUILayout.Button(label)) source.Send(new SelectWeaponCommand(weapon));
            }

            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("   move:", GUILayout.Width(44f));
            foreach (BodyMove move in Moves)
            {
                string name = MoveName(move);
                if (GUILayout.Button(move == source.Draft.BodyMove ? $"[{name}]" : name, GUILayout.Width(MoveButtonWidth))) SetBodyMove(source, move);
            }

            GUILayout.EndHorizontal();
            string refused = source.Draft.LastRejection != CommandRejection.None ? $"   refused: {source.Draft.LastRejection}" : string.Empty;
            GUILayout.Label("   " + PathLine(_preview[side]) + refused);
        }

        /// <summary>The HUD name of a move; the backward move says which D12 option it plays.</summary>
        private string MoveName(BodyMove move)
        {
            switch (move)
            {
                case BodyMove.None: return "none";
                case BodyMove.StepBack:
                    BodyMoveStats stats = LiveBodyMove(move);
                    return stats != null && stats.IsLeanInPlace ? "lean" : "back";
                default: return move.ToString().ToLowerInvariant();
            }
        }

        private static string PathLine(PathPreview preview)
        {
            if (preview == null || preview.Path.IsEmpty) return "no path (holds its weapon)";

            string notes = (preview.WasClampedByReach ? ", clamped to reach" : string.Empty) +
                           (preview.WasCutByInk ? ", cut where the ink ran out" : string.Empty) +
                           (preview.WasCutAtBreak ? ", cut at a too-sharp turn" : string.Empty);
            return string.Format(CultureInfo.InvariantCulture, "path {0:0} units, ink {1:0} of {2:0}{3}", preview.Path.LengthUnits,
                preview.InkSpentUnits, preview.InkBudgetUnits, notes);
        }

        /// <summary>A touch that lands on the sandbox panel, the readout or the debug overlay is a button press, not a stroke.</summary>
        private sealed class UiBlocker : ITouchBlocker
        {
            private readonly SandboxController _owner;

            public UiBlocker(SandboxController owner)
            {
                _owner = owner;
            }

            public bool Blocks(Vec2 screenPixels)
            {
                bool overUi = _owner.IsOverUi(screenPixels) || _owner.IsOverHud(screenPixels);

                // A touch that lands in the arena takes keyboard focus off any tuning text field, so the keys work again.
                if (!overUi) GUIUtility.keyboardControl = 0;
                return overUi;
            }
        }
    }
}
