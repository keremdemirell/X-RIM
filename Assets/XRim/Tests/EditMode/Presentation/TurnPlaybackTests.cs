using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using XRim.Config;
using XRim.Core;
using XRim.Presentation.Dummy;
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

        private sealed class TestEvent : MatchEvent
        {
            public TestEvent(SimTime time) : base(time)
            {
            }
        }
    }
}
