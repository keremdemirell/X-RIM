using System;
using System.Collections.Generic;
using UnityEngine;
using XRim.App;
using XRim.Config;
using XRim.Core;
using XRim.Input;
using XRim.DebugTools.Sandbox;
using XRim.Networking;
using XRim.Rules;
using XRim.Rules.Limbs;
using XRim.Rules.Match;

namespace XRim.DebugTools
{
    /// <summary>
    /// In-game debug panel (IMGUI, works with touch on device). Tabs: live tuning, execution playback
    /// (slow motion, frame step, re-simulate last turn), cheats, and the device (what it is, its screen, frame rate, what the
    /// simulation costs per turn, the fingers on the screen). On a phone the whole panel is scaled up from the screen's dpi, so its
    /// button and text are big enough to touch, and it keeps clear of a notch or cut-out.
    /// </summary>
    public sealed class DebugOverlay : MonoBehaviour
    {
        private const float Margin = 8f;
        private const float ToggleWidth = 90f;
        private const float ToggleHeight = 44f;
        private const float PanelWidth = 460f;

        /// <summary>A phone's dpi divided by this is how much the panel is enlarged (a desktop screen, near 96 dpi, is not).</summary>
        private const float ReferenceDpi = 200f;

        private const float MaxScale = 3f;
        private const int FrameWindow = 120;

        private static readonly string[] Tabs = { "Tuning", "Playback", "Cheats", "Device" };
        private static readonly string[] SideNames = { "Left", "Right" };
        private static readonly BodyPart[] Limbs = { BodyPart.LeftArm, BodyPart.RightArm, BodyPart.LeftLeg, BodyPart.RightLeg };

        private readonly TuningPanel _tuningPanel = new TuningPanel();
        private readonly PlaybackPanel _playbackPanel = new PlaybackPanel();
        private readonly DevicePanel _devicePanel = new DevicePanel();
        private readonly RollingStatistics _frameMilliseconds = new RollingStatistics(FrameWindow);
        private MatchBootstrap _bootstrap;
        private GameplayGizmos _gizmos;
        private bool _open;
        private int _tab;
        private int _cheatSide;
        private Vector2 _scroll;

        private void Start()
        {
            // Debug tooling may look up the scene's composition root once; gameplay code never does this.
            _bootstrap = FindAnyObjectByType<MatchBootstrap>();
            if (_bootstrap == null) return;
            _gizmos = gameObject.AddComponent<GameplayGizmos>();
            _gizmos.Init(_bootstrap);
            if (_bootstrap.Mode == MatchMode.Sandbox) gameObject.AddComponent<SandboxController>().Init(_bootstrap, this);
            else _bootstrap.AddTouchBlocker(new OverlayTouchBlocker(this)); // a tap on the Debug button is not a body-zone tap
        }

        private void Update() => _frameMilliseconds.Add(Time.unscaledDeltaTime * 1000.0);

        /// <summary>True when a screen point (screen pixels, y down) is on the Debug button or the open panel.</summary>
        internal bool Covers(Vector2 guiPoint)
        {
            float scale = Scale();
            Vector2 inGuiUnits = guiPoint / scale;
            if (ToggleRect(scale).Contains(inGuiUnits)) return true;
            return _open && PanelRect(scale).Contains(inGuiUnits);
        }

        /// <summary>How much bigger than a desktop screen the panel is drawn: a phone's pixels are small, its fingers are not.</summary>
        private static float Scale() => Screen.dpi > 0f ? Mathf.Clamp(Screen.dpi / ReferenceDpi, 1f, MaxScale) : 1f;

        /// <summary>What the screen's notch, cut-out and rounded corners cover now (pixels), so the button is never under one.</summary>
        private static ScreenInsets UnsafeEdges()
        {
            Rect safe = Screen.safeArea;
            return ScreenInsets.FromSafeArea(safe.x, safe.y, safe.width, safe.height, Screen.width, Screen.height);
        }

        // Both rectangles are in GUI units (screen pixels divided by the scale).
        private static Rect ToggleRect(float scale)
        {
            ScreenInsets edges = UnsafeEdges();
            return new Rect(Margin + edges.Left / scale, Margin + edges.Top / scale, ToggleWidth, ToggleHeight);
        }

        private static Rect PanelRect(float scale)
        {
            ScreenInsets edges = UnsafeEdges();
            float top = Margin * 2f + ToggleHeight + edges.Top / scale;
            float width = Math.Min(PanelWidth, Screen.width / scale - Margin * 2f - (edges.Left + edges.Right) / scale);
            return new Rect(Margin + edges.Left / scale, top, width, Screen.height / scale - top - Margin - edges.Bottom / scale);
        }

        /// <summary>One line of what this device reports, for the GDD §18 record: screen, refresh rate, frame-rate target, unsafe edges.</summary>
        private static string DisplayLine()
        {
            string target = Application.targetFrameRate < 0 ? "default" : Application.targetFrameRate.ToString();
            ScreenInsets edges = UnsafeEdges();
            return $"Display {Screen.width}x{Screen.height} · {Screen.currentResolution.refreshRateRatio.value:0.#} Hz · target {target} fps · " +
                   $"unsafe edges (px) {(edges.IsNone ? "none" : edges.ToString())}";
        }

        private void OnGUI()
        {
            Matrix4x4 previous = GUI.matrix;
            float scale = Scale();
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            try
            {
                DrawOverlay(scale);
            }
            finally
            {
                GUI.matrix = previous;
            }
        }

        private void DrawOverlay(float scale)
        {
            if (GUI.Button(ToggleRect(scale), _open ? "Close" : "Debug")) _open = !_open;
            if (!_open) return;

            GUILayout.BeginArea(PanelRect(scale), GUI.skin.box);
            GUILayout.Label(DisplayLine());
            _tab = GUILayout.Toolbar(_tab, Tabs);
            _scroll = GUILayout.BeginScrollView(_scroll);
            switch (_tab)
            {
                case 0:
                    DrawTuning();
                    break;
                case 1:
                    DrawPlayback();
                    break;
                case 2:
                    DrawCheats();
                    break;
                default:
                    _devicePanel.Draw(_bootstrap, _frameMilliseconds);
                    break;
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawTuning()
        {
            TuningProfile profile = _bootstrap != null ? _bootstrap.Tuning : null;
            if (profile == null)
            {
                GUILayout.Label("No MatchBootstrap with a TuningProfile in this scene.");
                return;
            }

            _tuningPanel.Draw(profile);
        }

        private void DrawPlayback() => _playbackPanel.Draw(_bootstrap, _gizmos);

        /// <summary>Touches on the Debug button or the open panel (screen pixels, y up) never reach a player's input.</summary>
        private sealed class OverlayTouchBlocker : ITouchBlocker
        {
            private readonly DebugOverlay _overlay;

            public OverlayTouchBlocker(DebugOverlay overlay)
            {
                _overlay = overlay;
            }

            public bool Blocks(Vec2 screenPixels) => _overlay != null && _overlay.Covers(new Vector2(screenPixels.X, Screen.height - screenPixels.Y));
        }

        /// <summary>
        /// Cheats act through public seams (the turn's contact handler, the clock, the settings snapshot), never through rule code. Force
        /// sever (Session 11): the chosen limb is cut at the start of the next turn through the real sever path.
        /// </summary>
        private void DrawCheats()
        {
            if (_bootstrap == null || _bootstrap.Authority == null)
            {
                GUILayout.Label("Start a match to use the cheats.");
                return;
            }

            GUILayout.Label("Force sever: cut a limb at the start of the next turn (it falls, a held weapon drops, debris sprays).");
            _cheatSide = GUILayout.Toolbar(_cheatSide, SideNames);
            Side side = _cheatSide == 0 ? Side.Left : Side.Right;
            PlanningWindow window = _bootstrap.Flow != null ? _bootstrap.Flow.CurrentWindow : null;
            FighterState fighter = window != null ? window.Board.State.Fighters[side] : null;
            IReadOnlyList<SeveredLimbRef> queued = _bootstrap.QueuedSevers;

            GUILayout.BeginHorizontal();
            foreach (BodyPart limb in Limbs)
            {
                bool gone = fighter != null && fighter.IsSevered(limb);
                bool waiting = Contains(queued, new SeveredLimbRef(side, limb));
                GUI.enabled = !gone && !waiting;
                string label = gone ? $"{limb} (off)" : waiting ? $"{limb} (next turn)" : limb.ToString();
                if (GUILayout.Button(label)) _bootstrap.QueueSever(side, limb);
                GUI.enabled = true;
            }

            GUILayout.EndHorizontal();
            GUILayout.Label(queued.Count == 0 ? "Nothing queued." : "Queued: " + string.Join(", ", queued));
            GUILayout.Space(Margin);
            GUILayout.Label("Planned: freeze planning timer, infinite ink, force stun, save/load scenario (board + both plans).");
        }

        private static bool Contains(IReadOnlyList<SeveredLimbRef> limbs, SeveredLimbRef limb)
        {
            for (int i = 0; i < limbs.Count; i++)
            {
                if (limbs[i] == limb) return true;
            }

            return false;
        }
    }
}
