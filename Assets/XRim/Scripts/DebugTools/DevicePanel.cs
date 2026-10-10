using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem.EnhancedTouch;
using XRim.App;
using XRim.Core;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

namespace XRim.DebugTools
{
    /// <summary>
    /// The Device tab (Session 10, GDD §18 target devices and performance budget): what this device is, what its screen reports, how
    /// the frames are going, what the hidden simulation costs per turn, and the fingers on the screen right now (for the multi-touch
    /// test between the body zone and the weapon zone). The report is plain text with a decimal point whatever the phone's language;
    /// "Log this report" writes it to the log (and so to the device log file) as one block.
    /// </summary>
    public sealed class DevicePanel
    {
        private const double MillisecondsPerSecond = 1000.0;
        private const double SlowFrameFraction = 0.95;

#if ENABLE_IL2CPP
        private const string ScriptingBackend = "IL2CPP";
#else
        private const string ScriptingBackend = "Mono";
#endif

        private readonly StringBuilder _text = new StringBuilder();
        private string _report = string.Empty;
        private int _mostFingers;

        public void Draw(MatchBootstrap bootstrap, RollingStatistics frameMilliseconds)
        {
            // Rebuilt once per layout pass, so the repaint that follows draws the same text.
            if (Event.current.type == EventType.Layout) _report = BuildReport(bootstrap, frameMilliseconds);
            GUILayout.Label(_report);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Log this report")) Debug.Log("[XRim] Device report\n" + _report);
            if (GUILayout.Button("Reset measurements"))
            {
                bootstrap?.SimulationTiming.Clear();
                frameMilliseconds.Clear();
                _mostFingers = 0;
            }

            GUILayout.EndHorizontal();
            GUILayout.Label("The game's log lines are also kept in " + DeviceLogWriter.FileName + " in the app's data folder on the device.");
        }

        private string BuildReport(MatchBootstrap bootstrap, RollingStatistics frameMilliseconds)
        {
            CultureInfo invariant = CultureInfo.InvariantCulture;
            _text.Clear();

            _text.AppendLine("DEVICE");
            _text.AppendFormat(invariant, "  {0} · {1}\n", SystemInfo.deviceModel, SystemInfo.operatingSystem);
            _text.AppendFormat(invariant, "  CPU {0}, {1} cores at {2} MHz · RAM {3} MB\n",
                SystemInfo.processorType, SystemInfo.processorCount, SystemInfo.processorFrequency, SystemInfo.systemMemorySize);
            _text.AppendFormat(invariant, "  GPU {0} ({1}), {2} MB\n", SystemInfo.graphicsDeviceName, SystemInfo.graphicsDeviceType, SystemInfo.graphicsMemorySize);
            _text.AppendFormat(invariant, "  battery {0} · build {1}, {2}{3}\n", BatteryText(), Application.version, ScriptingBackend,
                Debug.isDebugBuild ? ", development" : string.Empty);

            _text.AppendLine("DISPLAY");
            Rect safe = Screen.safeArea;
            ScreenInsets edges = ScreenInsets.FromSafeArea(safe.x, safe.y, safe.width, safe.height, Screen.width, Screen.height);
            _text.AppendFormat(invariant, "  {0}x{1} · {2:0.#} Hz · {3:0} dpi · {4}\n", Screen.width, Screen.height,
                Screen.currentResolution.refreshRateRatio.value, Screen.dpi, Screen.orientation);
            _text.AppendFormat(invariant, "  frame-rate target {0} · vSync {1} · unsafe edges (px) {2}\n",
                Application.targetFrameRate < 0 ? "default" : Application.targetFrameRate.ToString(invariant), QualitySettings.vSyncCount,
                edges.IsNone ? "none" : edges.ToString());

            _text.AppendLine("FRAMES (the last two seconds or so)");
            if (frameMilliseconds.WindowCount == 0)
            {
                _text.AppendLine("  no frames yet");
            }
            else
            {
                double mean = frameMilliseconds.WindowMean;
                _text.AppendFormat(invariant, "  {0:0.0} fps · frame mean {1:0.0} ms · 95% under {2:0.0} ms · worst {3:0.0} ms\n",
                    mean > 0.0 ? MillisecondsPerSecond / mean : 0.0, mean, frameMilliseconds.Percentile(SlowFrameFraction), frameMilliseconds.WindowMax);
            }

            _text.AppendLine("SIMULATION (the hidden turn, on this device)");
            _text.AppendLine("  " + (bootstrap != null ? bootstrap.SimulationTiming.Describe("ms") : "no match scene"));

            _text.AppendLine("TOUCH");
            AppendTouches(_text, invariant);
            return _text.ToString();
        }

        private void AppendTouches(StringBuilder text, CultureInfo invariant)
        {
            if (!EnhancedTouchSupport.enabled)
            {
                text.AppendLine("  touch support is off (no input reader running)");
                return;
            }

            var touches = Touch.activeTouches;
            _mostFingers = Mathf.Max(_mostFingers, touches.Count);
            text.AppendFormat(invariant, "  {0} finger(s) now · most at once {1}\n", touches.Count, _mostFingers);
            for (int i = 0; i < touches.Count; i++)
            {
                Touch touch = touches[i];
                text.AppendFormat(invariant, "  #{0} at ({1:0}, {2:0}) {3}\n", touch.touchId, touch.screenPosition.x, touch.screenPosition.y, touch.phase);
            }
        }

        private static string BatteryText()
        {
            float level = SystemInfo.batteryLevel; // -1 when the device does not say
            return level < 0f ? "unknown" : string.Format(CultureInfo.InvariantCulture, "{0:0}% ({1})", level * 100f, SystemInfo.batteryStatus);
        }
    }
}
