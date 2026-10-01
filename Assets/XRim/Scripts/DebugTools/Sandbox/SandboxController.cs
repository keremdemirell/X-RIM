using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using XRim.App;
using XRim.Config;
using XRim.Core;
using XRim.DebugTools.Diagnostics;
using XRim.Rules;
using XRim.Rules.Paths;
using XRim.Rules.Settings;
using XRim.Simulation;
using XRim.Simulation.Drivers;
using XRim.Simulation.Physics;
using XRim.Simulation.Unity2D;

namespace XRim.DebugTools.Sandbox
{
    /// <summary>
    /// The debug sandbox (MatchBootstrap in <see cref="MatchMode.Sandbox"/>): plan both sides with the mouse before touch
    /// input exists (Session 08). Pick a side, drag a path in its torso frame, pick its weapon, press Execute, watch the
    /// playback, then plan the next turn from the frozen board. Everything goes through two <see cref="SandboxPlanSource"/>s
    /// as real planning commands, so the rules apply exactly as in a match. The planning timer is frozen.
    /// </summary>
    internal sealed class SandboxController : MonoBehaviour
    {
        /// <summary>Raw points closer than this are skipped; the path rules resample anyway.</summary>
        private const float MinPointSpacingUnits = 1f;

        private const float HudMargin = 8f;
        private const float HudWidth = 560f;
        private const float HudHeight = 250f;
        private const string ReportFolderName = "Logs";
        private const string ReportFileName = "XRimFeelReport.txt";
        private const string SpriteShaderName = "Universal Render Pipeline/2D/Sprite-Unlit-Default";
        private const string FallbackShaderName = "Sprites/Default";

        private readonly List<Vec2> _stroke = new List<Vec2>();
        private readonly PerSide<BuiltPath> _built = new PerSide<BuiltPath>(null, null);
        private MatchBootstrap _bootstrap;
        private DebugOverlay _overlay;
        private PathBuilder _pathBuilder;
        private PerSide<SandboxPlanSource> _sources;
        private PerSide<SandboxPathDrawer> _drawers;
        private Side _activeSide = Side.Left;
        private bool _drawing;
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
            HandleMouse();
            UpdatePreviews();
        }

        private void OnDestroy()
        {
            if (_drawers == null) return;
            _drawers.Left.Destroy();
            _drawers.Right.Destroy();
        }

        /// <summary>Waits until the match scene has composed, then hands it the two plan sources.</summary>
        private void TryStart()
        {
            if (!_bootstrap.IsComposed || _bootstrap.HasStarted) return;

            _pathBuilder = new PathBuilder(_bootstrap.Policies);
            Material material = CreateLineMaterial();
            _drawers = PerSide<SandboxPathDrawer>.Create(side => new SandboxPathDrawer(_bootstrap.ArenaRoot, material, side));
            _sources = PerSide<SandboxPlanSource>.Create(side => new SandboxPlanSource(side));
            _bootstrap.StartMatch(new[] { _sources.Left, _sources.Right });
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
            if (keyboard.cKey.wasPressedThisFrame) _sources[_activeSide].ClearPath();
            IReadOnlyList<WeaponId> loadout = _bootstrap.Setup.Fighters[_activeSide].Loadout;
            if (keyboard.digit1Key.wasPressedThisFrame && loadout.Count > 0) _sources[_activeSide].SelectWeapon(loadout[0]);
            if (keyboard.digit2Key.wasPressedThisFrame && loadout.Count > 1) _sources[_activeSide].SelectWeapon(loadout[1]);
            if (keyboard.digit3Key.wasPressedThisFrame && loadout.Count > 2) _sources[_activeSide].SelectWeapon(loadout[2]);
        }

        private void HandleMouse()
        {
            Mouse mouse = Mouse.current;
            Camera view = Camera.main;
            if (mouse == null || view == null || !IsPlanning)
            {
                _drawing = false;
                return;
            }

            Vector2 screen = mouse.position.ReadValue();
            Vec2 local = ToTorsoFrame(screen, view, _activeSide);
            if (mouse.leftButton.wasPressedThisFrame && !IsOverUi(screen))
            {
                // Clicking into the arena takes keyboard focus away from any tuning text field, so the keys work again.
                GUIUtility.keyboardControl = 0;
                _drawing = true;
                _stroke.Clear();
                _stroke.Add(local);
            }
            else if (_drawing && mouse.leftButton.isPressed)
            {
                if (Vec2.Distance(_stroke[_stroke.Count - 1], local) >= MinPointSpacingUnits) _stroke.Add(local);
            }
            else if (_drawing)
            {
                _drawing = false;
                if (_stroke.Count >= 2) _sources[_activeSide].SetPath(new WeaponPath(_stroke));
            }
        }

        private bool IsOverUi(Vector2 screen)
        {
            var guiPoint = new Vector2(screen.x, Screen.height - screen.y);
            return GUIUtility.hotControl != 0 || _hudRect.Contains(guiPoint) || (_overlay != null && _overlay.Covers(guiPoint));
        }

        /// <summary>Screen pixels → arena units → the side's upright turn-start torso frame (GDD §6: paths are torso-relative).</summary>
        private Vec2 ToTorsoFrame(Vector2 screen, Camera view, Side side)
        {
            Vector3 world = view.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -view.transform.position.z));
            Vector3 inArena = _bootstrap.ArenaRoot.InverseTransformPoint(world);
            Vec2 arena = _bootstrap.Space.ToArena(new Vector2(inArena.x, inArena.y));
            return TorsoFrame.ToLocal(arena, Root(side), side);
        }

        private BodyPose Root(Side side) => TurnStartRoot.Of(_sources[side].Window.Board.Pose.Get(side));

        private void SwitchSide()
        {
            _drawing = false;
            _activeSide = _activeSide.Opponent();
        }

        private void Execute()
        {
            _drawing = false;
            _sources.Left.Ready();
            _sources.Right.Ready();
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

        /// <summary>Builds each side's path as the rules will (PathBuilder with the window's weapon tip and ink) and draws it.</summary>
        private void UpdatePreviews()
        {
            foreach (Side side in new[] { Side.Left, Side.Right })
            {
                SandboxPlanSource source = _sources[side];
                if (!source.IsPlanning)
                {
                    _built[side] = null;
                    _drawers[side].Clear();
                    continue;
                }

                IReadOnlyList<Vec2> stroke = _drawing && side == _activeSide ? _stroke : source.Stroke.Points;
                WeaponStats weapon = Weapon(source);
                _built[side] = weapon != null && HasMovement(stroke)
                    ? _pathBuilder.Build(WeaponPath.Empty, new WeaponPath(stroke), source.Window.WeaponTips[side].TipLocal(source.Weapon), weapon,
                        _bootstrap.RulesSettings.Paths)
                    : null;
                _drawers[side].Show(stroke, _built[side]?.Path, weapon != null ? weapon.InkThicknessUnits : 0f, Root(side), _bootstrap.Space);
            }
        }

        private WeaponStats Weapon(SandboxPlanSource source)
        {
            WeaponStats stats = _bootstrap.RulesSettings.FindWeapon(source.Weapon);
            return stats?.WithInkLengthMultiplier(source.Window.Constraints[source.Side].InkLengthMultiplier);
        }

        private static bool HasMovement(IReadOnlyList<Vec2> stroke)
        {
            for (int i = 1; i < stroke.Count; i++)
            {
                if (stroke[i] != stroke[0]) return true;
            }

            return false;
        }

        private void OnGUI()
        {
            if (_sources == null) return;
            _hudRect = new Rect(Screen.width - HudWidth - HudMargin, HudMargin, HudWidth, HudHeight);
            GUILayout.BeginArea(_hudRect, GUI.skin.box);
            if (IsPlanning) DrawPlanning();
            else GUILayout.Label("Executing and playing back… (Debug → Playback: slow motion, loop, re-simulate)");
            GUILayout.EndArea();
        }

        private void DrawPlanning()
        {
            GUILayout.Label($"X-RIM sandbox: turn {_sources.Left.Window.TurnIndex + 1}, planning (timer frozen)");
            GUILayout.BeginHorizontal();
            GUILayout.Label("Drawing for:");
            foreach (Side side in new[] { Side.Left, Side.Right })
            {
                string label = side == _activeSide ? $"[ {side.ToString().ToUpperInvariant()} ]" : side.ToString();
                if (GUILayout.Button(label) && side != _activeSide) SwitchSide();
            }

            GUILayout.Label("(Tab)");
            GUILayout.EndHorizontal();

            foreach (Side side in new[] { Side.Left, Side.Right })
            {
                DrawSide(side);
            }

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Clear path (C)")) _sources[_activeSide].ClearPath();
            if (GUILayout.Button("Execute (Space)")) Execute();
            GUILayout.EndHorizontal();
            GUILayout.Label("Drag in the arena to draw the active side's path. Weapons: 1/2/3. Feel report: F9. " +
                            "Simulation tuning applies from the next Execute; rules tuning from the next match.");
            if (_diagnosticsMessage.Length > 0) GUILayout.Label(_diagnosticsMessage);
        }

        private void DrawSide(Side side)
        {
            SandboxPlanSource source = _sources[side];
            GUILayout.BeginHorizontal();
            GUILayout.Label(side + ":", GUILayout.Width(44f));
            foreach (WeaponId weapon in _bootstrap.Setup.Fighters[side].Loadout)
            {
                string label = weapon == source.Weapon ? $"[{weapon.Value}]" : weapon.Value;
                if (GUILayout.Button(label)) source.SelectWeapon(weapon);
            }

            GUILayout.EndHorizontal();
            GUILayout.Label("   " + PathLine(_built[side]) + (source.LastRejection.Length > 0 ? "   " + source.LastRejection : string.Empty));
        }

        private static string PathLine(BuiltPath built)
        {
            if (built == null) return "no path (holds its weapon)";
            InkedPath inked = built.Inked;
            string notes = (built.WasClampedByReach ? ", clamped to reach" : string.Empty) +
                           (inked.WasCut ? ", cut where the ink ran out" : string.Empty) +
                           (built.WasCutAtBreak ? ", cut at a too-sharp turn" : string.Empty);
            return string.Format(CultureInfo.InvariantCulture, "path {0:0} units, ink {1:0} of {2:0}{3}", inked.Path.LengthUnits,
                inked.Ink.CostUnits, inked.BudgetUnits, notes);
        }

        private static Material CreateLineMaterial()
        {
            Shader shader = Shader.Find(SpriteShaderName);
            if (shader == null) shader = Shader.Find(FallbackShaderName);
            return shader != null ? new Material(shader) : null;
        }
    }
}
