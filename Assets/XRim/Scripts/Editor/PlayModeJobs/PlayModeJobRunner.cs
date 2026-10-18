using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

namespace XRim.Editor.PlayModeJobs
{
    /// <summary>
    /// Runs an <see cref="IPlayModeJob"/> from an Editor menu (Session 16). The hidden physics world needs Play mode (its scene is made with a
    /// runtime-only Unity call), so the runner:
    /// <list type="number">
    /// <item>offers to save the open scenes and remembers them;</item>
    /// <item>opens a temporary empty scene and enters Play mode (about a second with domain reload off);</item>
    /// <item>runs the job in slices of at most <see cref="SliceMilliseconds"/> per Editor update, with a progress bar and a Cancel button
    /// (stopping Play mode by hand cancels too);</item>
    /// <item>lets the job write its output, leaves Play mode, reopens the remembered scenes and shows the job's message.</item>
    /// </list>
    /// Safety (designer, 2026-10-18): a job does not start, and a running job stops, while the system drive or the project drive has less
    /// than 10 GB free (<see cref="DiskSpaceGuard"/>, checked about once a second), and a job stops after <see cref="MaxJobSeconds"/> however far
    /// it got, so nothing can run on unattended.
    /// The request and the result live in <see cref="SessionState"/>, so they survive a domain reload if the project ever turns it back on;
    /// the running job lives in a <see cref="JobSession"/> held only by the Editor's callbacks (no mutable statics).
    /// </summary>
    [InitializeOnLoad]
    internal static class PlayModeJobRunner
    {
        /// <summary>The longest one Editor update spends on the job, so the Editor and its progress bar stay responsive.</summary>
        private const long SliceMilliseconds = 100L;

        /// <summary>A request older than this when Play mode starts is stale (Play mode never started for it) and is dropped.</summary>
        private const double RequestTimeoutSeconds = 120.0;

        /// <summary>30 minutes: the longest any job runs before it stops itself (technical safety limit).</summary>
        private const double MaxJobSeconds = 30.0 * 60.0;

        /// <summary>How often a running job checks the free disk space.</summary>
        private const double DiskCheckIntervalSeconds = 1.0;

        private const string KindKey = "XRim.PlayModeJob.Kind";
        private const string ArgumentsKey = "XRim.PlayModeJob.Arguments";
        private const string RequestedAtKey = "XRim.PlayModeJob.RequestedAt";
        private const string ReturnScenesKey = "XRim.PlayModeJob.ReturnScenes";
        private const string ResultTitleKey = "XRim.PlayModeJob.ResultTitle";
        private const string ResultMessageKey = "XRim.PlayModeJob.ResultMessage";

        static PlayModeJobRunner()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        /// <summary>Starts a job of this kind (<see cref="PlayModeJobKinds"/>); returns false when it cannot start now (Play mode, or the save was cancelled).</summary>
        public static bool Start(string kind, string arguments)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("X-RIM", "Stop Play mode first: this tool starts Play mode by itself.", "OK");
                return false;
            }

            if (!PlayModeJobKinds.IsKnown(kind)) throw new ArgumentException($"No Play-mode job of kind '{kind}'.", nameof(kind));
            string disk = DiskSpaceGuard.Problem();
            if (disk != null)
            {
                EditorUtility.DisplayDialog("X-RIM", $"Not started: {disk}. Free some space, then try again.", "OK");
                return false;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;

            SessionState.SetString(ReturnScenesKey, JsonUtility.ToJson(ReturnScenes.Capture()));
            SessionState.SetString(KindKey, kind);
            SessionState.SetString(ArgumentsKey, arguments ?? string.Empty);
            SessionState.SetString(RequestedAtKey, EditorApplication.timeSinceStartup.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorApplication.EnterPlaymode();
            return true;
        }

        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            switch (change)
            {
                case PlayModeStateChange.EnteredPlayMode:
                    BeginRequestedJob();
                    break;
                case PlayModeStateChange.EnteredEditMode:
                    ShowResult();
                    break;
            }
        }

        private static void BeginRequestedJob()
        {
            string kind = SessionState.GetString(KindKey, string.Empty);
            if (kind.Length == 0) return; // an ordinary Play session
            string arguments = SessionState.GetString(ArgumentsKey, string.Empty);
            double.TryParse(SessionState.GetString(RequestedAtKey, "0"), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out double requestedAt);
            SessionState.EraseString(KindKey);
            SessionState.EraseString(ArgumentsKey);
            SessionState.EraseString(RequestedAtKey);
            if (EditorApplication.timeSinceStartup - requestedAt > RequestTimeoutSeconds) return;

            IPlayModeJob job;
            try
            {
                job = PlayModeJobKinds.Create(kind, arguments);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                SetResult("X-RIM", "The job could not start: " + exception.Message);
                EditorApplication.ExitPlaymode();
                return;
            }

            new JobSession(job).Begin();
        }

        private static void SetResult(string title, string message)
        {
            SessionState.SetString(ResultTitleKey, title);
            SessionState.SetString(ResultMessageKey, message);
        }

        private static void ShowResult()
        {
            string message = SessionState.GetString(ResultMessageKey, string.Empty);
            if (message.Length == 0) return;
            string title = SessionState.GetString(ResultTitleKey, "X-RIM");
            SessionState.EraseString(ResultTitleKey);
            SessionState.EraseString(ResultMessageKey);

            string scenes = SessionState.GetString(ReturnScenesKey, string.Empty);
            SessionState.EraseString(ReturnScenesKey);
            if (scenes.Length > 0) JsonUtility.FromJson<ReturnScenes>(scenes).Restore();

            AssetDatabase.Refresh(); // files the job wrote under Assets/ show up in the Project window
            Debug.Log($"[XRim] {title}: {message}");
            EditorUtility.DisplayDialog(title, message, "OK");
        }

        /// <summary>
        /// One running job: steps it from the Editor's update, finishes it, and leaves Play mode. Stopping Play mode by hand cancels it; so do
        /// the safety checks (a drive under 10 GB free, the 30-minute limit).
        /// </summary>
        private sealed class JobSession
        {
            private IPlayModeJob _job;
            private bool _cancelled;
            private string _failure;
            private string _safetyStop;
            private double _startedAt;
            private double _nextDiskCheckAt;

            public JobSession(IPlayModeJob job)
            {
                _job = job;
            }

            public void Begin()
            {
                _startedAt = EditorApplication.timeSinceStartup;
                _nextDiskCheckAt = _startedAt;
                EditorApplication.update += Tick;
                EditorApplication.playModeStateChanged += OnPlayModeChanged;
            }

            /// <summary>Why the job must stop now for safety, or null: a drive running out of room, or the time limit.</summary>
            private string SafetyProblem()
            {
                double now = EditorApplication.timeSinceStartup;
                if (now - _startedAt > MaxJobSeconds)
                    return $"the job ran past its {MaxJobSeconds / 60.0:0}-minute limit";
                if (now < _nextDiskCheckAt) return null;
                _nextDiskCheckAt = now + DiskCheckIntervalSeconds;
                return DiskSpaceGuard.Problem();
            }

            private void OnPlayModeChanged(PlayModeStateChange change)
            {
                if (change != PlayModeStateChange.ExitingPlayMode || _job == null) return;
                _cancelled = true; // Play mode was stopped by hand
                Finish();
            }

            private void Tick()
            {
                if (_job == null) return;
                _safetyStop = SafetyProblem();
                if (_safetyStop != null)
                {
                    Debug.LogWarning($"[XRim] {_job.Title} stopped for safety: {_safetyStop}.");
                    _cancelled = true;
                    Finish();
                    EditorApplication.ExitPlaymode();
                    return;
                }

                bool more = true;
                Stopwatch watch = Stopwatch.StartNew();
                try
                {
                    while (more && watch.ElapsedMilliseconds < SliceMilliseconds) more = _job.Step();
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                    _failure = exception.Message;
                    more = false;
                }

                if (more && EditorUtility.DisplayCancelableProgressBar(_job.Title, _job.Status, _job.Progress))
                {
                    _cancelled = true;
                    more = false;
                }

                if (more) return;
                Finish();
                EditorApplication.ExitPlaymode();
            }

            private void Finish()
            {
                EditorApplication.update -= Tick;
                EditorApplication.playModeStateChanged -= OnPlayModeChanged;
                IPlayModeJob job = _job;
                _job = null;
                EditorUtility.ClearProgressBar();
                if (job == null) return;

                string message;
                try
                {
                    message = job.Finish(_cancelled || _failure != null);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                    message = "Writing the results failed: " + exception.Message;
                }

                if (_failure != null) message = $"Stopped by an error: {_failure} (see the Console).\n\n{message}";
                else if (_safetyStop != null) message = $"Stopped for safety: {_safetyStop}.\n\n{message}";
                else if (_cancelled) message = "Cancelled.\n\n" + message;
                SetResult(job.Title, message);
            }
        }

        /// <summary>The scenes that were open before the job, to reopen after it (untitled scenes cannot be reopened and are skipped).</summary>
        [Serializable]
        private sealed class ReturnScenes
        {
            public List<string> Paths = new List<string>();
            public int ActiveIndex;

            public static ReturnScenes Capture()
            {
                var scenes = new ReturnScenes();
                Scene active = SceneManager.GetActiveScene();
                for (int i = 0; i < SceneManager.sceneCount; i++)
                {
                    Scene scene = SceneManager.GetSceneAt(i);
                    if (string.IsNullOrEmpty(scene.path)) continue;
                    if (scene == active) scenes.ActiveIndex = scenes.Paths.Count;
                    scenes.Paths.Add(scene.path);
                }

                return scenes;
            }

            public void Restore()
            {
                for (int i = 0; i < Paths.Count; i++)
                {
                    Scene opened = EditorSceneManager.OpenScene(Paths[i], i == 0 ? OpenSceneMode.Single : OpenSceneMode.Additive);
                    if (i == ActiveIndex) SceneManager.SetActiveScene(opened);
                }
            }
        }
    }
}
