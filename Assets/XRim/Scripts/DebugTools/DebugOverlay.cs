using UnityEngine;
using XRim.App;
using XRim.Config;
using XRim.DebugTools.Sandbox;

namespace XRim.DebugTools
{
    /// <summary>
    /// In-game debug panel (IMGUI, works with touch on device). Tabs: live tuning, execution playback
    /// (slow motion, frame step, re-simulate last turn), and cheats.
    /// </summary>
    public sealed class DebugOverlay : MonoBehaviour
    {
        private const float Margin = 8f;
        private const float ToggleWidth = 90f;
        private const float ToggleHeight = 44f;
        private const float PanelWidth = 460f;

        private static readonly string[] Tabs = { "Tuning", "Playback", "Cheats" };

        private readonly TuningPanel _tuningPanel = new TuningPanel();
        private readonly PlaybackPanel _playbackPanel = new PlaybackPanel();
        private MatchBootstrap _bootstrap;
        private GameplayGizmos _gizmos;
        private bool _open;
        private int _tab;
        private Vector2 _scroll;

        private void Start()
        {
            // Debug tooling may look up the scene's composition root once; gameplay code never does this.
            _bootstrap = FindAnyObjectByType<MatchBootstrap>();
            if (_bootstrap == null) return;
            _gizmos = gameObject.AddComponent<GameplayGizmos>();
            _gizmos.Init(_bootstrap);
            if (_bootstrap.Mode == MatchMode.Sandbox) gameObject.AddComponent<SandboxController>().Init(_bootstrap, this);
        }

        /// <summary>True when a screen point (GUI coordinates, y down) is on the Debug button or the open panel.</summary>
        internal bool Covers(Vector2 guiPoint)
        {
            if (ToggleRect.Contains(guiPoint)) return true;
            return _open && PanelRect.Contains(guiPoint);
        }

        private static Rect ToggleRect => new Rect(Margin, Margin, ToggleWidth, ToggleHeight);

        private static Rect PanelRect
        {
            get
            {
                float top = Margin * 2f + ToggleHeight;
                return new Rect(Margin, top, PanelWidth, Screen.height - top - Margin);
            }
        }

        private void OnGUI()
        {
            if (GUI.Button(ToggleRect, _open ? "Close" : "Debug")) _open = !_open;
            if (!_open) return;

            GUILayout.BeginArea(PanelRect, GUI.skin.box);
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
                default:
                    DrawCheats();
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

        private static void DrawCheats()
        {
            // Placeholder: cheats act through public seams (clock, settings snapshot), never through rule code.
            GUILayout.Label("Planned: freeze planning timer, infinite ink, force stun or sever, hot-seat toggle,\n" +
                            "save/load scenario (board + both plans).");
        }
    }
}
