using System;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace XRim.DebugTools
{
    /// <summary>
    /// Keeps the game's own log lines (the ones starting "[XRim]", such as the simulation time of every turn) and every error in a text
    /// file in the app's data folder on the device, so a measurement outlives the system log's small buffer and can be copied off
    /// the phone without a debugger (GDD §18: the performance numbers are recorded). Development builds and the Editor only, like every
    /// debug tool. A file left bigger than <see cref="MaxFileBytesAtStart"/> by earlier runs starts over, and one run writes at most
    /// <see cref="MaxRunBytes"/>, so an error that repeats every frame cannot fill a phone.
    /// </summary>
    internal sealed class DeviceLogWriter : MonoBehaviour
    {
        public const string FileName = "XRimDeviceLog.txt";

        private const long MaxFileBytesAtStart = 2L * 1024L * 1024L;
        private const long MaxRunBytes = 1L * 1024L * 1024L;
        private const string GameLinePrefix = "[XRim]";
        private const int MaxStackTraceCharacters = 600;
        private const string TimeFormat = "HH:mm:ss.fff";
        private const string DateTimeFormat = "yyyy-MM-dd HH:mm:ss";

        private readonly object _lock = new object();
        private StreamWriter _writer;
        private long _runBytes;
        private bool _full;

        private void OnEnable()
        {
            string path = Path.Combine(Application.persistentDataPath, FileName);
            try
            {
                var existing = new FileInfo(path);
                FileMode mode = existing.Exists && existing.Length > MaxFileBytesAtStart ? FileMode.Create : FileMode.Append;
                var stream = new FileStream(path, mode, FileAccess.Write, FileShare.ReadWrite);
                lock (_lock)
                {
                    _writer = new StreamWriter(stream) { AutoFlush = true };
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[XRim] Could not open the device log {path}: {exception.Message}");
                return;
            }

            Application.logMessageReceivedThreaded += OnLogMessage;
            Write($"=== run started {Now(DateTimeFormat)} · {SystemInfo.deviceModel} · {SystemInfo.operatingSystem} · " +
                  $"{Application.productName} {Application.version} ===");
            Debug.Log($"[XRim] Device log: {path}");
        }

        private void OnDisable()
        {
            Application.logMessageReceivedThreaded -= OnLogMessage;
            lock (_lock)
            {
                _writer?.Dispose();
                _writer = null;
            }
        }

        // Called from any thread that logs.
        private void OnLogMessage(string message, string stackTrace, LogType type)
        {
            bool gameLine = message.StartsWith(GameLinePrefix, StringComparison.Ordinal);
            bool problem = type == LogType.Error || type == LogType.Exception || type == LogType.Assert;
            if (!gameLine && !problem) return;

            string line = $"{Now(TimeFormat)} {Label(type)} {message}";
            if (problem && !string.IsNullOrEmpty(stackTrace))
            {
                string trimmed = stackTrace.Length > MaxStackTraceCharacters ? stackTrace.Substring(0, MaxStackTraceCharacters) + "…" : stackTrace;
                line += "\n" + trimmed.TrimEnd();
            }

            Write(line);
        }

        private void Write(string line)
        {
            lock (_lock)
            {
                if (_writer == null || _full) return;

                _runBytes += line.Length + Environment.NewLine.Length;
                if (_runBytes > MaxRunBytes)
                {
                    _full = true;
                    _writer.WriteLine($"{Now(TimeFormat)} --- this run's log limit ({MaxRunBytes / 1024} KB) is reached: nothing more is written ---");
                    return;
                }

                _writer.WriteLine(line);
            }
        }

        private static string Now(string format) => DateTime.Now.ToString(format, CultureInfo.InvariantCulture);

        private static string Label(LogType type)
        {
            switch (type)
            {
                case LogType.Error: return "ERROR";
                case LogType.Exception: return "EXCEPTION";
                case LogType.Assert: return "ASSERT";
                case LogType.Warning: return "WARN";
                default: return "INFO";
            }
        }
    }
}
