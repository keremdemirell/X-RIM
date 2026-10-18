using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using XRim.App;
using XRim.Core;
using XRim.Presentation.Feel;
using XRim.Presentation.Playback;
using XRim.Rules.Events;
using XRim.Simulation.Execution;
using XRim.Simulation.Recording;
using XRim.Simulation.Scenarios;

namespace XRim.DebugTools
{
    /// <summary>
    /// Debug overlay tab for the turn being shown (ARCHITECTURE §10): playback speed 0.05×–2×, pause, frame step, scrub,
    /// loop, replay, and re-simulating the last turn with the current tuning. Playback only bends time on screen; the
    /// turn was resolved up front, so nothing here changes an outcome. Looping or pausing holds the match on this turn.
    /// Session 13: the instant replay of the last turn on demand, the hit-zone and legendary-moment gizmos, and how long the last
    /// turns took on screen against the feel layer's plan. Session 16: save the last turn as a scenario, replay a saved one (with its own
    /// settings or the current tuning, display only), and in the Editor add one to the regression tests.
    /// </summary>
    internal sealed class PlaybackPanel
    {
        private const float MinSpeed = 0.05f;
        private const float MaxSpeed = 2f;
        private const float ButtonWidth = 64f;
        private const string ReachedMark = "► ";
        private const string PendingMark = "   ";

        /// <summary>How many saved scenarios the tab lists, the newest first.</summary>
        private const int ListedScenarios = 8;

        /// <summary>How many differences a replay's message names before "and n more".</summary>
        private const int NamedDifferences = 3;

        private static readonly float[] SpeedPresets = { 0.05f, 0.1f, 0.25f, 0.5f, 1f, 2f };

        private string _message = string.Empty;
        private ScenarioStore _store; // made on first use: the overlay builds this panel before Unity allows path queries
        private IReadOnlyList<string> _savedScenarios = Array.Empty<string>();
        private string _scenarioName = string.Empty;
        private string _scenarioMessage = string.Empty;

        public void Draw(MatchBootstrap bootstrap, GameplayGizmos gizmos)
        {
            if (bootstrap == null || !bootstrap.IsComposed)
            {
                GUILayout.Label("No composed match in this scene (needs MatchBootstrap; see the Console).");
                return;
            }

            TurnPlayback playback = bootstrap.Playback;
            TimelinePlayer player = playback.Player;
            TurnResult turn = playback.Current;
            if (turn == null)
            {
                GUILayout.Label("No turn played yet.");
                DrawScenarios(bootstrap);
                return;
            }

            DrawSummary(bootstrap, turn);
            DrawSpeed(player);
            DrawTransport(playback, player);
            DrawScrub(player, turn.Timeline);
            DrawResimulate(bootstrap);
            DrawInstantReplay(bootstrap);
            DrawScenarios(bootstrap);
            if (gizmos != null)
            {
                gizmos.ShowContacts = GUILayout.Toggle(gizmos.ShowContacts, "Contact gizmos: normals, clash angle and powers, time-to-impact");
                gizmos.ShowHitZones = GUILayout.Toggle(gizmos.ShowHitZones, "Hit zones: each body outlined, zone multiplier, limb damage");
                gizmos.ShowMoments = GUILayout.Toggle(gizmos.ShowMoments, "Legendary moment: where and why the replay picks it");
            }

            DrawPacing(bootstrap);
            bool showProgress = playback.IsPlaying || player.CurrentTime > SimTime.Zero;
            DrawOutcomes(turn, player.CurrentTime, showProgress);
            DrawContacts(turn, player.CurrentTime, showProgress);
        }

        private static void DrawSummary(MatchBootstrap bootstrap, TurnResult turn)
        {
            TurnTimeline timeline = turn.Timeline;
            int steps = timeline.Frames.Count > 0 ? timeline.Frames[timeline.Frames.Count - 1].Step : 0;
            string resimulated = turn == bootstrap.LastResimulation ? "   [re-simulated with the current tuning, display only]"
                : turn == bootstrap.LastScenarioReplay ? "   [a saved scenario, display only]" : string.Empty;
            GUILayout.Label(string.Format(CultureInfo.InvariantCulture, "Turn {0}: {1}, {2} steps ({3:0.000} s), simulated in {4:0.0} ms{5}",
                turn.TurnIndex + 1, turn.EndReason, steps, timeline.Duration.Seconds, bootstrap.LastSimulationMilliseconds, resimulated));

            // Session 13: what the feel layer does to this turn on screen (hitstops, slow motion, the camera settling).
            FeelDirector feel = bootstrap.Feel;
            if (feel == null) return;
            GUILayout.Label(string.Format(CultureInfo.InvariantCulture, "On screen: {0:0.00} s so far; planned {1}",
                bootstrap.Playback.PlayRealSeconds, feel.Pacing));
        }

        /// <summary>Session 13: the last turn's instant replay now (its legendary moment, or its hardest hit), as between turns only.</summary>
        private void DrawInstantReplay(MatchBootstrap bootstrap)
        {
            if (bootstrap.Replay != null && bootstrap.Replay.IsPlaying)
            {
                GUILayout.Label($"Instant replay playing: {bootstrap.Replay.Moment}");
                if (GUILayout.Button("Skip the replay")) bootstrap.Replay.Skip();
                return;
            }

            GUI.enabled = bootstrap.CanReplayLastTurn;
            if (GUILayout.Button("Instant replay of the last turn (its moment, or its hardest hit)")) bootstrap.ReplayLastTurn();
            GUI.enabled = true;
            if (!bootstrap.CanReplayLastTurn) GUILayout.Label("   (between turns only: while planning, with nothing playing)");
        }

        /// <summary>
        /// Session 16: save the last simulated turn as a scenario (board, plans, settings and what it did), and replay a saved one between
        /// turns: with its own settings it must repeat its recording; with the current tuning the message says what the tuning changed.
        /// </summary>
        private void DrawScenarios(MatchBootstrap bootstrap)
        {
            if (_store == null)
            {
                _store = ScenarioStore.CreateDefault();
                RefreshScenarios();
            }

            GUILayout.Label("Scenarios: save the last turn, replay a saved one (display only; the match carries on)");
            GUILayout.BeginHorizontal();
            GUILayout.Label("Name", GUILayout.Width(ButtonWidth));
            _scenarioName = GUILayout.TextField(_scenarioName);
            GUI.enabled = bootstrap.CanCaptureLastTurn;
            if (GUILayout.Button("Save the last turn", GUILayout.Width(ButtonWidth * 2.5f))) SaveLastTurn(bootstrap);
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            if (_scenarioMessage.Length > 0) GUILayout.Label(_scenarioMessage);

            GUILayout.BeginHorizontal();
            GUILayout.Label($"Saved in {_store.Folder}: {_savedScenarios.Count}");
            if (GUILayout.Button("Refresh", GUILayout.Width(ButtonWidth * 1.5f))) RefreshScenarios();
            GUILayout.EndHorizontal();
            for (int i = 0; i < Math.Min(_savedScenarios.Count, ListedScenarios); i++)
            {
                DrawSavedScenario(bootstrap, _savedScenarios[i]);
            }

            if (_savedScenarios.Count > 0 && !bootstrap.CanPlayScenario)
                GUILayout.Label("   (a scenario plays between turns only: while planning, with nothing playing)");
        }

        private void DrawSavedScenario(MatchBootstrap bootstrap, string path)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(Path.GetFileNameWithoutExtension(path));
            GUI.enabled = bootstrap.CanPlayScenario;
            if (GUILayout.Button("Replay", GUILayout.Width(ButtonWidth * 1.5f))) ReplayScenario(bootstrap, path, false);
            if (GUILayout.Button("Replay with current tuning", GUILayout.Width(ButtonWidth * 3.5f))) ReplayScenario(bootstrap, path, true);
            GUI.enabled = true;
#if UNITY_EDITOR
            if (GUILayout.Button("Add to regression tests", GUILayout.Width(ButtonWidth * 3.5f))) AddToRegressionTests(path);
#endif
            GUILayout.EndHorizontal();
        }

        private void RefreshScenarios() => _savedScenarios = _store.List();

        private void SaveLastTurn(MatchBootstrap bootstrap)
        {
            string name = _scenarioName.Trim();
            if (name.Length == 0) name = DefaultScenarioName(bootstrap);
            try
            {
                Scenario scenario = bootstrap.CaptureLastTurn(name, out List<string> differences);
                if (scenario == null)
                {
                    _scenarioMessage = "No turn has been simulated yet.";
                    return;
                }

                string path = _store.Save(scenario);
                RefreshScenarios();
                _scenarioMessage = differences.Count == 0
                    ? $"Saved {Path.GetFileName(path)}: it replays exactly as the turn played."
                    : $"Saved {Path.GetFileName(path)}, but it replays differently from the turn as played ({Name(differences)}). A debug cheat " +
                      "(forced sever, KO or stun) was probably on, which a scenario does not keep, or the scene's dummy prefabs are older than the " +
                      "tuning (XRim/Setup/Build Placeholder Dummies). The file keeps the replay's outcome.";
                Debug.Log($"[XRim] Scenario saved to {path}. {_scenarioMessage}");
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException ||
                                              exception is NotSupportedException || exception is FormatException)
            {
                _scenarioMessage = "Could not save the scenario: " + exception.Message;
                Debug.LogWarning("[XRim] " + _scenarioMessage);
            }
        }

        private void ReplayScenario(MatchBootstrap bootstrap, string path, bool withCurrentTuning)
        {
            string file = Path.GetFileNameWithoutExtension(path);
            Scenario scenario;
            try
            {
                scenario = _store.Load(path);
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException ||
                                              exception is NotSupportedException || exception is FormatException)
            {
                _scenarioMessage = $"Could not load {file}: {exception.Message}";
                return;
            }

            TurnResult replayed = bootstrap.PlayScenario(scenario, withCurrentTuning);
            if (replayed == null)
            {
                _scenarioMessage = "A scenario plays between turns only (while planning, with nothing playing).";
                return;
            }

            if (scenario.Expected == null)
            {
                _scenarioMessage = $"Playing {file} (it has no recorded outcome to compare with).";
                return;
            }

            List<string> differences = ScenarioComparison.Differences(scenario.Expected, ScenarioOutcome.Of(replayed), ScenarioTolerance.Exact);
            if (withCurrentTuning)
            {
                _scenarioMessage = differences.Count == 0
                    ? $"Playing {file} with the current tuning: the turn plays out exactly as recorded."
                    : $"Playing {file} with the current tuning, which changes it: {Name(differences)}.";
            }
            else
            {
                _scenarioMessage = differences.Count == 0
                    ? $"Playing {file}: it repeats its recording exactly."
                    : $"Playing {file}, which no longer repeats its recording (the code changed since it was saved): {Name(differences)}.";
            }
        }

#if UNITY_EDITOR
        /// <summary>Editor only: the scenario joins the PlayMode regression tests (a file in the project, to commit).</summary>
        private void AddToRegressionTests(string path)
        {
            try
            {
                _store.CopyToRegressionTests(path);
                UnityEditor.AssetDatabase.Refresh();
                _scenarioMessage = $"Added {Path.GetFileName(path)} to {ScenarioStore.RegressionFolderInProject}: the PlayMode regression tests " +
                                   "replay it from now on. Commit it with the rest.";
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException ||
                                              exception is NotSupportedException || exception is FormatException ||
                                              exception is InvalidOperationException)
            {
                _scenarioMessage = "Not added: " + exception.Message;
            }
        }
#endif

        private static string DefaultScenarioName(MatchBootstrap bootstrap)
        {
            TurnInput last = bootstrap.LastTurnInput;
            int turn = last != null ? last.Board.State.TurnIndex + 1 : 0;
            return string.Format(CultureInfo.InvariantCulture, "turn {0} {1}", turn, bootstrap.Mode);
        }

        /// <summary>The first few differences in one sentence, and how many more there are.</summary>
        private static string Name(List<string> differences)
        {
            int named = Math.Min(differences.Count, NamedDifferences);
            string text = string.Join("; ", differences.GetRange(0, named));
            return differences.Count > named ? $"{text}; and {differences.Count - named} more" : text;
        }

        /// <summary>Session 13: the last turns' time on screen against the plan; the rest is mostly the camera settling.</summary>
        private static void DrawPacing(MatchBootstrap bootstrap)
        {
            if (bootstrap.RecentPacing.Count == 0) return;
            GUILayout.Label("Last turns on screen: turn + slow motion + hitstops + the rest (camera settling, frame rounding)");
            foreach (PlayedTurnPacing played in bootstrap.RecentPacing)
            {
                PacingBreakdown planned = played.Planned;
                GUILayout.Label(string.Format(CultureInfo.InvariantCulture, "   Turn {0}: {1:0.00} s = {2:0.000} + {3:0.00} + {4:0.00} + {5:0.00}",
                    played.TurnIndex + 1, played.OnScreenSeconds, planned.TurnSeconds, planned.SlowMotionSeconds, planned.HitStopSeconds,
                    played.UnplannedSeconds));
            }
        }

        private static void DrawSpeed(TimelinePlayer player)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(string.Format(CultureInfo.InvariantCulture, "Speed {0:0.00}×", player.PlaybackSpeed), GUILayout.Width(ButtonWidth * 1.5f));
            player.PlaybackSpeed = GUILayout.HorizontalSlider(player.PlaybackSpeed, MinSpeed, MaxSpeed);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            foreach (float preset in SpeedPresets)
            {
                if (GUILayout.Button(preset.ToString("0.##", CultureInfo.InvariantCulture) + "×")) player.PlaybackSpeed = preset;
            }

            GUILayout.EndHorizontal();
        }

        private static void DrawTransport(TurnPlayback playback, TimelinePlayer player)
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(player.IsPaused ? "Resume" : "Pause")) player.IsPaused = !player.IsPaused;
            if (GUILayout.Button("◄ frame"))
            {
                player.IsPaused = true;
                player.StepFrames(-1);
            }

            if (GUILayout.Button("frame ►"))
            {
                player.IsPaused = true;
                player.StepFrames(1);
            }

            if (GUILayout.Button("Replay")) playback.Replay();
            player.Loop = GUILayout.Toggle(player.Loop, "Loop");
            GUILayout.EndHorizontal();
            if (player.Loop || player.IsPaused) GUILayout.Label("The match waits on this turn while it loops or is paused.");
        }

        private static void DrawScrub(TimelinePlayer player, TurnTimeline timeline)
        {
            float duration = (float)timeline.Duration.Seconds;
            float now = (float)player.CurrentTime.Seconds;
            GUILayout.BeginHorizontal();
            GUILayout.Label(string.Format(CultureInfo.InvariantCulture, "{0:0.0} / {1:0.0} ms  frame {2}/{3}", now * 1000f, duration * 1000f,
                player.CurrentFrameIndex + 1, timeline.Frames.Count), GUILayout.Width(ButtonWidth * 3.5f));
            float scrubbed = GUILayout.HorizontalSlider(now, 0f, Mathf.Max(duration, float.Epsilon));
            GUILayout.EndHorizontal();
            if (Mathf.Approximately(scrubbed, now)) return;

            // Scrubbing holds the picture where it was dragged to.
            player.IsPaused = true;
            player.Seek(SimTime.FromSeconds(scrubbed));
        }

        private void DrawResimulate(MatchBootstrap bootstrap)
        {
            if (GUILayout.Button("Re-simulate the last turn with the current tuning"))
            {
                _message = bootstrap.ResimulateLastTurn()
                    ? "Re-simulated (display only: the match carries on from the original result)."
                    : "Nothing to re-simulate yet.";
            }

            if (_message.Length > 0) GUILayout.Label(_message);
        }

        /// <summary>The rules' verdicts: hits with their damage, clashes, blocks, cancelled attacks, stuns and staggers, KOs.</summary>
        private static void DrawOutcomes(TurnResult turn, SimTime now, bool showProgress)
        {
            int count = 0;
            foreach (MatchEvent matchEvent in turn.Timeline.Events)
            {
                if (MatchEventText.IsOutcome(matchEvent)) count++;
            }

            GUILayout.Label($"Hits, clashes, blocks and outcomes: {count} (► = playback has reached it)");
            foreach (MatchEvent matchEvent in turn.Timeline.Events)
            {
                if (!MatchEventText.IsOutcome(matchEvent)) continue;
                bool reached = showProgress && matchEvent.Time <= now;
                GUILayout.Label((reached ? ReachedMark : PendingMark) + MatchEventText.Describe(matchEvent));
            }
        }

        private static void DrawContacts(TurnResult turn, SimTime now, bool showProgress)
        {
            int count = 0;
            foreach (MatchEvent matchEvent in turn.Timeline.Events)
            {
                if (matchEvent is ContactEvent) count++;
            }

            GUILayout.Label($"Contacts: {count} (earliest first; ► = playback has reached it)");
            if (count == 0) return;
            GUILayout.Label(ContactText.Header);
            foreach (MatchEvent matchEvent in turn.Timeline.Events)
            {
                if (!(matchEvent is ContactEvent contact)) continue;
                bool reached = showProgress && contact.Time <= now;
                GUILayout.Label((reached ? ReachedMark : PendingMark) + ContactText.Describe(contact.Contact, turn.FinalBoard.State));
            }
        }
    }
}
