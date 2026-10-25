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
using XRim.Rules.Settings;
using XRim.Simulation;
using XRim.Simulation.Recording;
using XRim.Simulation.Settings;
using XRim.Simulation.Unity2D;
using Object = UnityEngine.Object;

namespace XRim.Tests.PlayMode.App
{
    /// <summary>
    /// The first playable end to end (Session 09): the real match composition (<see cref="MatchBootstrap"/> with the project's tuning,
    /// the hidden Unity physics world, the authority, the flow, playback with hitstop and the HUDs) runs three turns planned by scripted
    /// sources, each next planning phase opening only after the last turn's playback.
    /// </summary>
    public sealed class MatchScenePlayModeTests
    {
        private const int Turns = 3;
        private const float ThrustUnits = 200f;

        /// <summary>Playback only (it never changes a turn): keeps the test short.</summary>
        private const float PlaybackSpeedUp = 4f;

        private const float TimeoutSeconds = 60f;
        private const float SceneUnloadTimeoutSeconds = 5f;

        private static readonly BodyMove[] LeftMoves = { BodyMove.Lunge, BodyMove.Crouch, BodyMove.None };
        private static readonly BodyMove[] RightMoves = { BodyMove.None, BodyMove.StepBack, BodyMove.Jump };

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
        public IEnumerator ThreeTurns_PlannedByScripts_RunThroughTheRealMatchScene()
        {
            TuningProfile tuning = ProjectTuning.Load();
            if (tuning == null) Assert.Ignore("No tuning profile: run XRim/Setup/Create Default Tuning Assets first.");

            MatchBootstrap bootstrap = CreateScene(tuning);
            yield return null; // Start: the scene composes and shows the mode picker

            Assert.That(bootstrap.IsComposed, Is.True);
            Assert.That(bootstrap.Authority, Is.Null, "no match before a mode is picked");

            bootstrap.PrepareMatch(MatchMode.HumanVsBot);
            var phases = new List<ClientPhase>();
            var windows = new List<PlanningWindow>();
            var resolved = new List<TurnResult>();
            var playbackFinishedAt = new Dictionary<int, double>();
            BoardSnapshot start = null;
            bootstrap.Flow.PhaseChanged += phases.Add;
            bootstrap.Authority.MatchStarted += info => start = info.InitialBoard;
            bootstrap.Authority.PlanningStarted += windows.Add;
            bootstrap.Authority.TurnResolved += resolved.Add;
            bootstrap.Playback.Finished += turn => playbackFinishedAt[turn.TurnIndex] = bootstrap.AuthorityNowSeconds;
            var left = new ScriptedPlanSource(Side.Left, window => Script(window, Side.Left, LeftMoves));
            var right = new ScriptedPlanSource(Side.Right, window => Script(window, Side.Right, RightMoves));

            bootstrap.StartMatch(new IPlanSource[] { left, right });
            bootstrap.Playback.Player.PlaybackSpeed = PlaybackSpeedUp;
            float deadline = Time.realtimeSinceStartup + TimeoutSeconds;
            yield return new WaitUntil(() => windows.Count > Turns || bootstrap.Flow.Phase == ClientPhase.MatchOver ||
                                             Time.realtimeSinceStartup > deadline);

            Assert.That(resolved.Count, Is.GreaterThanOrEqualTo(Turns), "three turns resolved in time");
            for (int turn = 0; turn < Turns; turn++)
            {
                Assert.That(resolved[turn].TurnIndex, Is.EqualTo(turn));
            }

            // Each turn: planning, plans locked, playback, waiting for the next window (or the end of the match).
            var expected = new List<ClientPhase>();
            for (int turn = 0; turn < Turns; turn++)
            {
                expected.AddRange(new[] { ClientPhase.Planning, ClientPhase.WaitingForAuthority, ClientPhase.Playback });
                if (turn < Turns - 1) expected.Add(ClientPhase.WaitingForAuthority);
            }

            Assert.That(phases.GetRange(0, expected.Count), Is.EqualTo(expected));

            // The authority opened each next planning phase only once the last turn had been played back.
            for (int turn = 0; turn + 1 < windows.Count && turn < Turns; turn++)
            {
                Assert.That(playbackFinishedAt.ContainsKey(turn), Is.True, $"turn {turn + 1} was played back");
                Assert.That(OpenedSeconds(windows[turn + 1]), Is.GreaterThanOrEqualTo(playbackFinishedAt[turn]),
                    $"turn {turn + 2}'s timer started after turn {turn + 1}'s playback");
            }

            // The scripted plans really reached the simulation: turn 1's lunge carried the left dummy toward the opponent.
            Assert.That(left.Results[0].Accepted, Is.True, "the lunge was accepted");
            float lunge = bootstrap.RulesSettings.FindBodyMove(BodyMove.Lunge).DisplacementUnits.X;
            Assert.That(Torso(resolved[0].FinalBoard, Side.Left), Is.GreaterThan(Torso(start, Side.Left) + lunge * 0.5f));
        }

        private MatchBootstrap CreateScene(TuningProfile tuning)
        {
            var issues = new List<string>();
            RulesSettings rules = tuning.BuildRulesSettings(issues);
            SimulationSettings simulation = tuning.BuildSimulationSettings(issues);
            ArenaSpace space = tuning.ArenaSpace;
            Ragdoll six = Template(RagdollSegmentation.SixBodies, rules, simulation, space);
            Ragdoll ten = Template(RagdollSegmentation.TenBodies, rules, simulation, space);

            var arenaRoot = new GameObject("ArenaRoot");
            _created.Add(arenaRoot);
            var holder = new GameObject("MatchBootstrap");
            holder.SetActive(false);
            _created.Add(holder);
            var bootstrap = holder.AddComponent<MatchBootstrap>();
            bootstrap.Configure(tuning, six, ten, arenaRoot.transform, null, MatchMode.HumanVsBot, MatchStartup.WaitForCaller);
            holder.SetActive(true);
            return bootstrap;
        }

        private Ragdoll Template(RagdollSegmentation segmentation, RulesSettings rules, SimulationSettings simulation, ArenaSpace space)
        {
            Ragdoll ragdoll = PlaceholderRagdollBuilder.Build(new RagdollBuildSpec(simulation.Ragdoll, rules.Paths, segmentation, space,
                rules.Weapons, null));
            ragdoll.gameObject.SetActive(false);
            _created.Add(ragdoll.gameObject);
            return ragdoll;
        }

        /// <summary>This turn's body move, then a straight thrust forward from the weapon tip (the D3 lead-in costs nothing).</summary>
        private static IEnumerable<PlanningCommand> Script(PlanningWindow window, Side side, BodyMove[] moves)
        {
            yield return new SetBodyMoveCommand(moves[Math.Min(window.TurnIndex, moves.Length - 1)]);
            WeaponId weapon = window.Board.State.Fighters[side].CurrentWeapon;
            Vec2 tip = window.WeaponTips[side].TipLocal(weapon);
            yield return new SetPathCommand(new WeaponPath(new[] { tip, tip + new Vec2(ThrustUnits, 0f) }));
        }

        /// <summary>When the planning phase opened: its deadline less the longer of the two sides' planning times.</summary>
        private static double OpenedSeconds(PlanningWindow window) =>
            window.DeadlineSeconds - Math.Max(window.Constraints[Side.Left].PlanningDurationSeconds,
                window.Constraints[Side.Right].PlanningDurationSeconds);

        private static float Torso(BoardSnapshot board, Side side) => board.Pose.Get(side).Get(BodyPart.Torso).PositionUnits.X;
    }
}
