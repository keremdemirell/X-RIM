using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using XRim.App;
using XRim.Config;
using XRim.Core;
using XRim.Networking;
using XRim.Rules;
using XRim.Rules.Paths;
using XRim.Rules.Planning;
using XRim.Simulation.Recording;
using XRim.Simulation.Scenarios;
using Object = UnityEngine.Object;

namespace XRim.Tests.PlayMode.App
{
    /// <summary>
    /// Saving and replaying a turn in the real match scene (Session 16, the Playback tab's Save and Replay): a turn played by scripts is
    /// captured as a scenario that replays exactly as it played, survives its file's text, and plays again on screen during the next
    /// planning phase, display only: the planning clock stands still and the board being planned comes back.
    /// </summary>
    public sealed class ScenarioCapturePlayModeTests
    {
        private const float ThrustUnits = 200f;
        private const float PlaybackSpeedUp = 4f;
        private const float TimeoutSeconds = 60f;
        private const float SceneUnloadTimeoutSeconds = 5f;

        private readonly List<Object> _created = new List<Object>();
        private SimulationMode2D _previousMode;
        private int _scenesBefore;

        [SetUp]
        public void SetUp()
        {
            _previousMode = Physics2D.simulationMode;
            _scenesBefore = SceneManager.sceneCount;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (Object created in _created)
            {
                if (created != null) Object.Destroy(created);
            }

            _created.Clear();
            float until = Time.realtimeSinceStartup + SceneUnloadTimeoutSeconds;
            yield return new WaitUntil(() => SceneManager.sceneCount <= _scenesBefore || Time.realtimeSinceStartup > until);
            Physics2D.simulationMode = _previousMode;
        }

        [UnityTest]
        public IEnumerator ALastTurn_SavedAndReplayed_PlaysTheSameTurn_WithoutEatingPlanningTime()
        {
            TuningProfile tuning = ProjectTuning.Load();
            if (tuning == null) Assert.Ignore("No tuning profile: run XRim/Setup/Create Default Tuning Assets first.");

            MatchBootstrap bootstrap = TestMatchScene.Create(tuning, _created);
            yield return null; // Start: the scene composes
            bootstrap.PrepareMatch(MatchMode.HumanVsBot);
            Assert.That(bootstrap.CanCaptureLastTurn, Is.False, "nothing to save before a turn");

            var resolved = new List<TurnResult>();
            bootstrap.Authority.TurnResolved += resolved.Add;
            var left = new ScriptedPlanSource(Side.Left, window => Thrust(window, Side.Left, BodyMove.Lunge)) { HoldReady = SecondTurnOn };
            var right = new ScriptedPlanSource(Side.Right, window => Thrust(window, Side.Right, BodyMove.Crouch)) { HoldReady = SecondTurnOn };
            bootstrap.StartMatch(new IPlanSource[] { left, right });
            bootstrap.Playback.Player.PlaybackSpeed = PlaybackSpeedUp;

            float deadline = Time.realtimeSinceStartup + TimeoutSeconds;
            yield return new WaitUntil(() => (resolved.Count > 0 && bootstrap.CanPlayScenario) || Time.realtimeSinceStartup > deadline);
            Assert.That(bootstrap.CanPlayScenario, Is.True, "turn 2's planning phase opened, nothing playing");

            // Save: the scenario replays on its own dummy exactly as the turn played.
            Scenario saved = bootstrap.CaptureLastTurn("capture test", out List<string> differences);
            Assert.That(saved, Is.Not.Null);
            Assert.That(differences, Is.Empty, string.Join("\n", differences));
            Assert.That(saved.Expected, Is.Not.Null);
            Assert.That(ScenarioComparison.Differences(ScenarioOutcome.Of(resolved[0]), saved.Expected, ScenarioTolerance.Exact), Is.Empty);
            Assert.That(saved.Input.Board.State.TurnIndex, Is.EqualTo(0));

            // Through a file's text and back, then on screen during planning.
            Scenario loaded = ScenarioJson.Read(ScenarioJson.Write(saved, DateTime.Now));
            double clockBefore = bootstrap.AuthorityNowSeconds;
            TurnResult replayed = bootstrap.PlayScenario(loaded, false);
            Assert.That(replayed, Is.Not.Null);
            Assert.That(ScenarioComparison.Differences(loaded.Expected, ScenarioOutcome.Of(replayed), ScenarioTolerance.Exact), Is.Empty,
                "the saved scenario repeats its recording");
            Assert.That(bootstrap.Playback.IsPlaying, Is.True);
            Assert.That(bootstrap.CanPlayScenario, Is.False, "one thing plays at a time");

            deadline = Time.realtimeSinceStartup + TimeoutSeconds;
            yield return new WaitUntil(() => !bootstrap.Playback.IsPlaying || Time.realtimeSinceStartup > deadline);
            Assert.That(bootstrap.Playback.IsPlaying, Is.False, "the scenario finished playing");
            Assert.That(bootstrap.AuthorityNowSeconds, Is.EqualTo(clockBefore), "the planning clock stood still while it played");
            Assert.That(bootstrap.Flow.Phase, Is.EqualTo(ClientPhase.Planning), "the match still waits in turn 2's planning");
            Assert.That(bootstrap.Playback.ShownPose.Get(Side.Left).Get(BodyPart.Torso).PositionUnits,
                Is.EqualTo(bootstrap.Flow.CurrentWindow.Board.Pose.Get(Side.Left).Get(BodyPart.Torso).PositionUnits),
                "the board being planned is back on screen");
            Assert.That(resolved.Count, Is.EqualTo(1), "replaying a scenario resolved no turn of the match");

            left.ReleaseReady();
            right.ReleaseReady();
        }

        private static bool SecondTurnOn(PlanningWindow window) => window.TurnIndex >= 1;

        /// <summary>A body move, then a straight thrust forward from the weapon tip.</summary>
        private static IEnumerable<PlanningCommand> Thrust(PlanningWindow window, Side side, BodyMove move)
        {
            yield return new SetBodyMoveCommand(move);
            WeaponId weapon = window.Board.State.Fighters[side].CurrentWeapon;
            Vec2 tip = window.WeaponTips[side].TipLocal(weapon);
            yield return new SetPathCommand(new WeaponPath(new[] { tip, tip + new Vec2(ThrustUnits, 0f) }));
        }
    }
}
