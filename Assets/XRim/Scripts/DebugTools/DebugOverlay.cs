using UnityEngine;
using XRim.App;
using XRim.Config;
using XRim.DebugTools.Spike;

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
        private MatchBootstrap _bootstrap;
        private SpikeHarness _spike;
        private bool _open;
        private int _tab;
        private Vector2 _scroll;

        private void Start()
        {
            // Debug tooling may look up the scene's composition root (or the spike harness) once; gameplay code never does this.
            _bootstrap = FindAnyObjectByType<MatchBootstrap>();
            _spike = FindAnyObjectByType<SpikeHarness>();
        }

        private void OnGUI()
        {
            if (GUI.Button(new Rect(Margin, Margin, ToggleWidth, ToggleHeight), _open ? "Close" : "Debug")) _open = !_open;
            if (!_open) return;

            float top = Margin * 2f + ToggleHeight;
            GUILayout.BeginArea(new Rect(Margin, top, PanelWidth, Screen.height - top - Margin), GUI.skin.box);
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
            if (profile == null && _spike != null) profile = _spike.Tuning;
            if (profile == null)
            {
                GUILayout.Label("No MatchBootstrap or spike harness with a TuningProfile in this scene.");
                return;
            }

            _tuningPanel.Draw(profile);
        }

        private static void DrawPlayback()
        {
            // Placeholder: binds to the TimelinePlayer once the match loop exists.
            GUILayout.Label("Planned: playback speed 0.05×–2×, pause, frame step, scrub, loop the last turn,\n" +
                            "and re-simulate the last turn with the current tuning.");
        }

        private static void DrawCheats()
        {
            // Placeholder: cheats act through public seams (clock, settings snapshot), never through rule code.
            GUILayout.Label("Planned: freeze planning timer, infinite ink, force stun or sever, hot-seat toggle,\n" +
                            "save/load scenario (board + both plans).");
        }
    }
}
