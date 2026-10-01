using System.Globalization;
using UnityEngine;
using XRim.App;
using XRim.Core;
using XRim.Presentation.Playback;
using XRim.Rules.Events;
using XRim.Simulation.Execution;
using XRim.Simulation.Recording;

namespace XRim.DebugTools
{
    /// <summary>
    /// Debug overlay tab for the turn being shown (ARCHITECTURE §10): playback speed 0.05×–2×, pause, frame step, scrub,
    /// loop, replay, and re-simulating the last turn with the current tuning. Playback only bends time on screen; the
    /// turn was resolved up front, so nothing here changes an outcome. Looping or pausing holds the match on this turn.
    /// </summary>
    internal sealed class PlaybackPanel
    {
        private const float MinSpeed = 0.05f;
        private const float MaxSpeed = 2f;
        private const float ButtonWidth = 64f;
        private const string ReachedMark = "► ";
        private const string PendingMark = "   ";

        private static readonly float[] SpeedPresets = { 0.05f, 0.1f, 0.25f, 0.5f, 1f, 2f };

        private string _message = string.Empty;

        public void Draw(MatchBootstrap bootstrap)
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
                return;
            }

            DrawSummary(bootstrap, turn);
            DrawSpeed(player);
            DrawTransport(playback, player);
            DrawScrub(player, turn.Timeline);
            DrawResimulate(bootstrap);
            DrawContacts(turn, player.CurrentTime, playback.IsPlaying || player.CurrentTime > SimTime.Zero);
        }

        private static void DrawSummary(MatchBootstrap bootstrap, TurnResult turn)
        {
            TurnTimeline timeline = turn.Timeline;
            int steps = timeline.Frames.Count > 0 ? timeline.Frames[timeline.Frames.Count - 1].Step : 0;
            string resimulated = turn == bootstrap.LastResimulation ? "   [re-simulated with the current tuning, display only]" : string.Empty;
            GUILayout.Label(string.Format(CultureInfo.InvariantCulture, "Turn {0}: {1}, {2} steps ({3:0.000} s), simulated in {4:0.0} ms{5}",
                turn.TurnIndex + 1, turn.EndReason, steps, timeline.Duration.Seconds, bootstrap.LastSimulationMilliseconds, resimulated));
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
