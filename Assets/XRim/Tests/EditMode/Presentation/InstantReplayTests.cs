using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using XRim.Config;
using XRim.Core;
using XRim.Presentation.Dummy;
using XRim.Presentation.Feel;
using XRim.Presentation.Moments;
using XRim.Presentation.Playback;
using XRim.Rules;
using XRim.Rules.Events;
using XRim.Rules.Settings;
using XRim.Simulation.Recording;

namespace XRim.Tests.EditMode.Presentation
{
    /// <summary>
    /// The instant replay (Session 13; §17: a local instant replay only): it plays the stored turn again from just before its moment to just
    /// after it, slower, the moment's events firing again, and ends by itself or at once when skipped, telling the match once.
    /// </summary>
    public sealed class InstantReplayTests
    {
        private const float FrameSeconds = 1f / 60f;

        private readonly List<Object> _created = new List<Object>();
        private readonly List<MatchEvent> _heard = new List<MatchEvent>();
        private TurnPlayback _playback;
        private FeelDirector _feel;
        private InstantReplay _replay;
        private int _finished;

        [SetUp]
        public void SetUp()
        {
            // NUnit runs every test of a fixture on one instance: start each from nothing.
            _finished = 0;
            _heard.Clear();
            var views = PerSide<DummyView>.Create(side =>
            {
                var view = Track(new GameObject(side + "View")).AddComponent<DummyView>();
                view.Configure(new Transform[BodyParts.Count], new Transform[BodyParts.Count]);
                return view;
            });
            _playback = new TurnPlayback(views, new ArenaSpace(ArenaSpaceConfig.DefaultWorldUnitsPerArenaUnit));
            _feel = new FeelDirector(Track(ScriptableObject.CreateInstance<FeelConfig>()));
            _playback.Pacer = _feel;
            _playback.AddReactor(_feel);
            _playback.AddReactor(new Recorder(_heard));
            _replay = new InstantReplay(_playback, _feel, null);
            _replay.Finished += () => _finished++;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object created in _created) Object.DestroyImmediate(created);
            _created.Clear();
        }

        [Test]
        public void TheReplay_PlaysFromJustBeforeTheMoment_AndEndsJustAfterIt_ByItself()
        {
            HitLandedEvent early = PlayedTurn.Hit(Side.Right, 0.1, 5f);
            HitLandedEvent moment = PlayedTurn.Hit(Side.Right, 0.6, 5f);
            TurnResult turn = PlayedTurn.Create(GddStartingValues.CreateRulesSettings(), 0, early, moment);
            var settings = new ReplaySettings { LeadSeconds = 0.2f, TailSeconds = 0.2f, Speed = 0.5f };

            _replay.Begin(turn, new LegendaryMoment(MomentKind.CounterHit, moment.Time, Side.Left, Vec2.Zero, "test"), settings);
            Assert.That(_replay.IsPlaying, Is.True);
            Assert.That(_playback.Player.CurrentTime.Seconds, Is.EqualTo(0.4).Within(1e-6), "0.2 s before the moment");

            for (int frame = 0; frame < 600 && _replay.IsPlaying; frame++)
            {
                _playback.Advance(FrameSeconds);
                _replay.Advance();
            }

            Assert.That(_finished, Is.EqualTo(1));
            Assert.That(_heard, Is.EqualTo(new MatchEvent[] { moment }), "only the moment fires again, not the hit before the stretch");
            Assert.That(_playback.Player.CurrentTime.Seconds, Is.LessThan(PlayedTurn.Seconds), "it ended at its tail, before the turn's end");
        }

        [Test]
        public void ASkip_EndsTheReplayAtOnce()
        {
            HitLandedEvent moment = PlayedTurn.Hit(Side.Right, 0.6, 5f);
            TurnResult turn = PlayedTurn.Create(GddStartingValues.CreateRulesSettings(), 0, moment);
            _replay.Begin(turn, new LegendaryMoment(MomentKind.Crush, moment.Time, Side.Left, Vec2.Zero, "test"), new ReplaySettings { LeadSeconds = 0.3f });

            _replay.Skip();
            _replay.Skip();

            Assert.That(_replay.IsPlaying, Is.False);
            Assert.That(_finished, Is.EqualTo(1), "told once");
        }

        [Test]
        public void ACancelledReplay_TellsNobody()
        {
            HitLandedEvent moment = PlayedTurn.Hit(Side.Right, 0.6, 5f);
            TurnResult turn = PlayedTurn.Create(GddStartingValues.CreateRulesSettings(), 0, moment);
            _replay.Begin(turn, new LegendaryMoment(MomentKind.Crush, moment.Time, Side.Left, Vec2.Zero, "test"), new ReplaySettings());

            _replay.Cancel();

            Assert.That(_replay.IsPlaying, Is.False);
            Assert.That(_finished, Is.EqualTo(0));
        }

        private T Track<T>(T created) where T : Object
        {
            _created.Add(created);
            return created;
        }

        private sealed class Recorder : IMatchEventReactor
        {
            private readonly List<MatchEvent> _heard;

            public Recorder(List<MatchEvent> heard)
            {
                _heard = heard;
            }

            public void OnMatchEvent(MatchEvent matchEvent) => _heard.Add(matchEvent);
        }
    }
}
