using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using XRim.Config;
using XRim.Core;
using XRim.Presentation.Dummy;
using XRim.Presentation.Feel;
using XRim.Presentation.Playback;
using XRim.Rules;
using XRim.Rules.Events;
using XRim.Rules.Match;
using XRim.Rules.Settings;
using XRim.Simulation.Physics;
using XRim.Simulation.Recording;

namespace XRim.Tests.EditMode.Presentation
{
    /// <summary>Playback tells the client exactly once that a turn has been shown, so the next planning phase can start.</summary>
    public sealed class TurnPlaybackTests
    {
        private readonly List<GameObject> _created = new List<GameObject>();
        private TurnPlayback _playback;
        private int _finished;

        [SetUp]
        public void SetUp()
        {
            _finished = 0;
            var views = PerSide<DummyView>.Create(side =>
            {
                var view = new GameObject(side + "View").AddComponent<DummyView>();
                _created.Add(view.gameObject);
                view.Configure(new Transform[BodyParts.Count], new Transform[BodyParts.Count]);
                return view;
            });
            _playback = new TurnPlayback(views, new ArenaSpace(ArenaSpaceConfig.DefaultWorldUnitsPerArenaUnit));
            _playback.Finished += _ => _finished++;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject created in _created) Object.DestroyImmediate(created);
            _created.Clear();
        }

        private static TurnResult Result(double seconds)
        {
            RulesSettings settings = GddStartingValues.CreateRulesSettings();
            BoardSnapshot board = TestData.CreateInitialBoard(settings);
            var recorder = new TimelineRecorder();
            if (seconds > 0.0)
            {
                recorder.RecordFrame(0, SimTime.Zero, new PoseSnapshot());
                recorder.RecordFrame(1, SimTime.FromSeconds(seconds), new PoseSnapshot());
            }

            var report = new ExecutionReport(board.State, new PerSide<SimTime?>(null, null), false);
            return new TurnResult(0, recorder.Build(), board, report);
        }

        [Test]
        public void Finished_FiresOnceWhenPlaybackReachesTheEnd()
        {
            _playback.Play(Result(0.1));

            _playback.Advance(0.05f);
            Assert.That(_finished, Is.EqualTo(0));
            _playback.Advance(0.1f);
            _playback.Advance(0.1f);

            Assert.That(_finished, Is.EqualTo(1));
            Assert.That(_playback.IsPlaying, Is.False);
        }

        [Test]
        public void ThePoseOnScreen_IsKept_ForTheDebugOverlays()
        {
            Assert.That(_playback.ShownPose, Is.Null, "nothing shown yet");
            BoardSnapshot board = Result(0.1).FinalBoard;

            _playback.ShowBoard(board);

            Assert.That(_playback.ShownPose, Is.SameAs(board.Pose));
        }

        [Test]
        public void PausedPlayback_NeverFinishes()
        {
            _playback.Play(Result(0.1));
            _playback.Player.IsPaused = true;

            _playback.Advance(1f);

            Assert.That(_finished, Is.EqualTo(0));
        }

        [Test]
        public void Looping_HoldsTheTurn_UntilTheLoopIsTurnedOff()
        {
            _playback.Play(Result(0.1));
            _playback.Player.Loop = true;

            _playback.Advance(0.06f);
            _playback.Advance(0.06f); // reaches the end (0.1 s)
            _playback.Advance(0.06f); // starts over
            Assert.That(_finished, Is.EqualTo(0), "a looping turn never finishes");
            Assert.That(_playback.Player.CurrentTime, Is.LessThan(SimTime.FromSeconds(0.1)), "it went round again");

            _playback.Player.Loop = false;
            _playback.Advance(1f);
            Assert.That(_finished, Is.EqualTo(1));
        }

        [Test]
        public void Replay_PlaysTheTurnAgainAndFinishesAgain()
        {
            _playback.Play(Result(0.1));
            _playback.Advance(1f);

            _playback.Replay();
            Assert.That(_playback.IsPlaying, Is.True);
            _playback.Advance(1f);

            Assert.That(_finished, Is.EqualTo(2));
        }

        [Test]
        public void Restart_FiresTheTurnsEventsAgain()
        {
            var timeline = new TurnTimeline(
                new[] { new TimelineFrame(0, SimTime.Zero, new PoseSnapshot()), new TimelineFrame(1, SimTime.FromSeconds(0.1), new PoseSnapshot()) },
                new MatchEvent[] { new TestEvent(SimTime.FromSeconds(0.05)) });
            var player = new TimelinePlayer();
            int fired = 0;
            player.EventReached += _ => fired++;
            player.Play(timeline);
            player.Advance(1f);

            player.Restart();
            player.Advance(1f);

            Assert.That(fired, Is.EqualTo(2));
        }

        [Test]
        public void EmptyTimeline_FinishesAtOnce()
        {
            _playback.Play(Result(0.0));

            Assert.That(_finished, Is.EqualTo(1));
        }

        // --- Feel: reactors and hitstop (Session 09) ---------------------------------------------------------------

        [Test]
        public void Reactors_HearEachEventOnce_AsPlaybackPassesIt()
        {
            var heard = new List<MatchEvent>();
            var reactor = new RecordingReactor(heard);
            _playback.AddReactor(reactor);
            HitLandedEvent hit = PlayedTurn.Hit(Side.Right, 0.4, 10f);
            _playback.Play(PlayedTurn.Create(GddStartingValues.CreateRulesSettings(), 0, hit));

            _playback.Advance(0.3f);
            Assert.That(heard, Is.Empty);
            _playback.Advance(0.2f);
            _playback.Advance(1f);
            Assert.That(heard, Is.EqualTo(new MatchEvent[] { hit }));

            _playback.RemoveReactor(reactor);
            _playback.Replay();
            _playback.Advance(1f);
            Assert.That(heard, Has.Count.EqualTo(1));
        }

        [Test]
        public void AHit_FreezesPlaybackForTheHitstop_AndTheTurnFinishesThatMuchLater()
        {
            FeelConfig feel = ScriptableObject.CreateInstance<FeelConfig>();
            try
            {
                var director = new FeelDirector(feel);
                _playback.Pacer = director;
                _playback.AddReactor(director);
                HitLandedEvent hit = PlayedTurn.Hit(Side.Right, 0.5, 10f);
                float freeze = HitStopSettings.From(feel).SecondsFor(hit);
                _playback.Play(PlayedTurn.Create(GddStartingValues.CreateRulesSettings(), 0, hit));

                _playback.Advance(0.5f); // reaches the hit
                SimTime atHit = _playback.Player.CurrentTime;
                _playback.Advance(freeze * 0.5f);
                Assert.That(_playback.Player.CurrentTime, Is.EqualTo(atHit), "frozen");

                _playback.Advance(freeze * 0.5f + 0.49f); // over a second of real time: 0.99 s of turn plus the freeze
                Assert.That(_finished, Is.EqualTo(0), "the freeze took real time, so the turn is not over yet");
                _playback.Advance(0.02f);
                Assert.That(_finished, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(feel);
            }
        }

        [Test]
        public void AHitOnTheLastFrame_StillGetsItsFreeze_BeforeTheTurnFinishes()
        {
            FeelConfig feel = ScriptableObject.CreateInstance<FeelConfig>();
            try
            {
                var director = new FeelDirector(feel);
                _playback.Pacer = director;
                _playback.AddReactor(director);
                HitLandedEvent hit = PlayedTurn.Hit(Side.Right, PlayedTurn.Seconds, 10f);
                _playback.Play(PlayedTurn.Create(GddStartingValues.CreateRulesSettings(), 0, hit));

                _playback.Advance((float)PlayedTurn.Seconds);
                Assert.That(_playback.Player.IsFinished, Is.True);
                Assert.That(_finished, Is.EqualTo(0), "held by the hitstop");

                _playback.Advance(HitStopSettings.From(feel).SecondsFor(hit));
                Assert.That(_finished, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(feel);
            }
        }

        private sealed class RecordingReactor : IMatchEventReactor
        {
            private readonly List<MatchEvent> _heard;

            public RecordingReactor(List<MatchEvent> heard)
            {
                _heard = heard;
            }

            public void OnMatchEvent(MatchEvent matchEvent) => _heard.Add(matchEvent);
        }

        private sealed class TestEvent : MatchEvent
        {
            public TestEvent(SimTime time) : base(time)
            {
            }
        }
    }
}
