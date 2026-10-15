using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using XRim.App;
using XRim.Config;
using XRim.Core;
using XRim.Networking;
using XRim.Presentation.Cameras;
using XRim.Rules;
using XRim.Rules.Events;
using XRim.Rules.Paths;
using XRim.Rules.Planning;
using XRim.Rules.Settings;
using XRim.Rules.Status;
using XRim.Simulation;
using XRim.Simulation.Recording;
using XRim.Simulation.Settings;
using XRim.Simulation.Unity2D;
using Object = UnityEngine.Object;

namespace XRim.Tests.PlayMode.App
{
    /// <summary>
    /// The feel pass end to end (Session 13): a whole match on the real match scene with every reactor attached (the camera director,
    /// the feel layer's freezes and slow motion, the mechanical effects, the sounds, the instant replay), its turns planned by scripts and
    /// shaped by the debug cheats: a sever (replayed), a stun, a replay asked for while planning with the timer frozen, a double KO into
    /// sudden death (replayed), and a KO that wins it (replayed). Unity fails the test on any error logged on the way.
    /// </summary>
    public sealed class FeelPassPlayModeTests
    {
        /// <summary>A short thrust: the dummies stay apart, so only the cheats decide the match.</summary>
        private const float ThrustUnits = 60f;

        /// <summary>Playback only (it never changes a turn): keeps the test short; freezes still hold their real time.</summary>
        private const float PlaybackSpeedUp = 3f;

        private const float TimeoutSeconds = 150f;
        private const float SceneUnloadTimeoutSeconds = 5f;

        // The match camera as XRim/Setup/Create Match Scene builds it: the whole arena, the floor near the bottom (world units).
        private const float CameraOrthographicSize = 5.75f;
        private const float CameraHeight = 5.25f;
        private const float CameraZ = -10f;
        private const string MainCameraTag = "MainCamera";

        /// <summary>The planning phase (0-based turn) during which the test asks for the last turn's replay.</summary>
        private const int ReplayAskedWhilePlanningTurn = 1;

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
        public IEnumerator AWholeMatch_WithEveryReactorAttached_PlaysToItsEnd_WithoutAnError()
        {
            TuningProfile tuning = ProjectTuning.Load();
            if (tuning == null) Assert.Ignore("No tuning profile: run XRim/Setup/Create Default Tuning Assets first.");

            GameObject arenaRoot = Track(new GameObject("ArenaRoot"));
            Camera camera = CreateCamera(arenaRoot.transform, out FixedSideCameraDirector director);
            MatchBootstrap bootstrap = CreateScene(tuning, arenaRoot.transform, camera);
            yield return null; // Start: the scene composes and shows the mode picker

            Assert.That(bootstrap.IsComposed, Is.True);
            Assert.That(bootstrap.Feel, Is.Not.Null, "the feel layer");
            Assert.That(bootstrap.Vfx, Is.Not.Null, "the mechanical effects");
            Assert.That(bootstrap.Audio, Is.Not.Null, "the sounds");
            Assert.That(bootstrap.Replay, Is.Not.Null, "the instant replay");
            float wholeArenaHeight = director.CurrentFraming.HeightUnits;

            bootstrap.PrepareMatch(MatchMode.HumanVsBot);
            bootstrap.InfiniteInk = true;
            Assert.That(bootstrap.Policies.InkCost, Is.InstanceOf<FreeInkCostModel>(), "the infinite-ink cheat swaps the ink policy");
            bootstrap.InfiniteInk = false;
            Assert.That(bootstrap.Policies.InkCost, Is.Not.InstanceOf<FreeInkCostModel>(), "and puts it back");

            var windows = new List<PlanningWindow>();
            var resolved = new List<TurnResult>();
            var legendary = new List<string>();
            int replaysEnded = 0;
            bootstrap.Authority.PlanningStarted += windows.Add;
            bootstrap.Authority.TurnResolved += resolved.Add;
            bootstrap.Replay.Finished += () => replaysEnded++;

            void OnLog(string message, string stackTrace, LogType type)
            {
                if (message.Contains("legendary moment")) legendary.Add(message);
            }

            var left = new ScriptedPlanSource(Side.Left, window => PlanAndCheat(bootstrap, window, Side.Left))
            {
                HoldReady = window => window.TurnIndex == ReplayAskedWhilePlanningTurn,
            };
            var right = new ScriptedPlanSource(Side.Right, window => PlanAndCheat(bootstrap, window, Side.Right));

            Application.logMessageReceived += OnLog;
            try
            {
                bootstrap.StartMatch(new IPlanSource[] { left, right });
                bootstrap.Playback.Player.PlaybackSpeed = PlaybackSpeedUp;
                float deadline = Time.realtimeSinceStartup + TimeoutSeconds;
                float closestFraming = float.MaxValue;
                int mostEffectPieces = 0;
                bool replayAsked = false;
                double frozenAtSeconds = 0.0;
                while (bootstrap.Flow.Phase != ClientPhase.MatchOver && Time.realtimeSinceStartup < deadline)
                {
                    closestFraming = Math.Min(closestFraming, director.CurrentFraming.HeightUnits);
                    mostEffectPieces = Math.Max(mostEffectPieces, bootstrap.Vfx.ActivePieces);
                    if (left.IsHoldingReady && !replayAsked && bootstrap.CanReplayLastTurn)
                    {
                        // Planning turn 2: freeze the timer and replay turn 1 on demand, then let the turn go.
                        bootstrap.IsPlanningTimerFrozen = true;
                        frozenAtSeconds = bootstrap.AuthorityNowSeconds;
                        Assert.That(bootstrap.ReplayLastTurn(), Is.True, "a replay asked for between turns");
                        Assert.That(bootstrap.Replay.IsPlaying, Is.True);
                        replayAsked = true;
                    }
                    else if (replayAsked && left.IsHoldingReady && !bootstrap.Replay.IsPlaying)
                    {
                        Assert.That(bootstrap.AuthorityNowSeconds, Is.EqualTo(frozenAtSeconds), "the frozen planning timer stood still");
                        bootstrap.IsPlanningTimerFrozen = false;
                        left.ReleaseReady();
                    }

                    yield return null;
                }

                Assert.That(bootstrap.Flow.Phase, Is.EqualTo(ClientPhase.MatchOver), "the match ended in time");
                Assert.That(bootstrap.Flow.Outcome.Winner, Is.EqualTo(Side.Left), "the right dummy's sudden-death KO lost it");

                // The cheats reached the simulation through the real paths.
                Assert.That(resolved[0].Timeline.Events.OfType<LimbSeveredEvent>().Any(e => e.Side == Side.Right && e.Part == BodyPart.LeftArm),
                    Is.True, "turn 1: the forced sever");
                Assert.That(windows[2].Constraints[Side.Left].Statuses, Has.Member(StatusKind.Stunned), "turn 3 is planned stunned");
                Assert.That(resolved[2].Timeline.Events.OfType<FighterDiedEvent>().Count(), Is.EqualTo(2), "turn 3: the forced double KO");
                Assert.That(windows[3].IsSuddenDeath, Is.True, "a double KO goes to sudden death (§3)");

                // Every reactor reacted: the replays (the sever, the double KO, the winning KO and the one asked for), the camera closing
                // in, the debris, the sounds (silent without clips), and the pacing record.
                Assert.That(replayAsked, Is.True, "the replay was asked for while planning");
                Assert.That(legendary.Count, Is.GreaterThanOrEqualTo(3), string.Join("\n", legendary));
                Assert.That(replaysEnded, Is.GreaterThanOrEqualTo(4));
                Assert.That(closestFraming, Is.LessThan(wholeArenaHeight), "the duel framing and the replay close in");
                Assert.That(mostEffectPieces, Is.GreaterThan(0), "the sever sprayed debris");
                Assert.That(bootstrap.Audio.PlayedCount + bootstrap.Audio.SilentCount, Is.GreaterThan(0), "sounds were asked for");
                Assert.That(bootstrap.RecentPacing.Count, Is.GreaterThan(0), "each played turn's pacing is recorded");
                Debug.Log($"[XRim] Feel pass: {resolved.Count} turns, {replaysEnded} replays, closest framing {closestFraming:0} units, " +
                          $"{mostEffectPieces} effect pieces at most, {bootstrap.Audio.PlayedCount} sounds played and " +
                          $"{bootstrap.Audio.SilentCount} silent.");
            }
            finally
            {
                Application.logMessageReceived -= OnLog;
            }
        }

        /// <summary>A short thrust each turn; the left side's script also queues this turn's cheat before it locks.</summary>
        private static IEnumerable<PlanningCommand> PlanAndCheat(MatchBootstrap bootstrap, PlanningWindow window, Side side)
        {
            if (side == Side.Left) QueueCheat(bootstrap, window);
            WeaponId weapon = window.Board.State.Fighters[side].CurrentWeapon;
            Vec2 tip = window.WeaponTips[side].TipLocal(weapon);
            yield return new SetPathCommand(new WeaponPath(new[] { tip, tip + new Vec2(ThrustUnits, 0f) }));
        }

        /// <summary>Turn 1 a sever, turn 2 a stun, turn 3 a double KO; in sudden death the right dummy is knocked out.</summary>
        private static void QueueCheat(MatchBootstrap bootstrap, PlanningWindow window)
        {
            if (window.IsSuddenDeath)
            {
                bootstrap.QueueKnockOut(Side.Right);
                return;
            }

            switch (window.TurnIndex)
            {
                case 0:
                    bootstrap.QueueSever(Side.Right, BodyPart.LeftArm);
                    break;
                case 1:
                    bootstrap.QueueStatus(Side.Left, StatusKind.Stunned);
                    break;
                case 2:
                    bootstrap.QueueDoubleKnockOut();
                    break;
            }
        }

        private Camera CreateCamera(Transform arenaRoot, out FixedSideCameraDirector director)
        {
            GameObject holder = Track(new GameObject("MatchCamera") { tag = MainCameraTag });
            holder.transform.position = new Vector3(0f, CameraHeight, CameraZ);
            var camera = holder.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = CameraOrthographicSize;
            director = holder.AddComponent<FixedSideCameraDirector>();
            director.SetArenaRoot(arenaRoot);
            return camera;
        }

        private MatchBootstrap CreateScene(TuningProfile tuning, Transform arenaRoot, Camera camera)
        {
            var issues = new List<string>();
            RulesSettings rules = tuning.BuildRulesSettings(issues);
            SimulationSettings simulation = tuning.BuildSimulationSettings(issues);
            ArenaSpace space = tuning.ArenaSpace;
            Ragdoll six = Template(RagdollSegmentation.SixBodies, rules, simulation, space);
            Ragdoll ten = Template(RagdollSegmentation.TenBodies, rules, simulation, space);

            GameObject holder = Track(new GameObject("MatchBootstrap"));
            holder.SetActive(false);
            var bootstrap = holder.AddComponent<MatchBootstrap>();
            bootstrap.Configure(tuning, six, ten, arenaRoot, null, MatchMode.HumanVsBot, true, null, camera);
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

        private T Track<T>(T created) where T : Object
        {
            _created.Add(created);
            return created;
        }
    }
}
