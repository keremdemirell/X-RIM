using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using XRim.App;
using XRim.Config;
using XRim.Core;
using XRim.Presentation.Dummy;
using XRim.Presentation.Feel;
using XRim.Presentation.Moments;
using XRim.Presentation.Playback;
using XRim.Rules;
using XRim.Rules.Events;
using XRim.Rules.Settings;
using XRim.Tests.EditMode.Presentation;

namespace XRim.Tests.EditMode.App
{
    /// <summary>While an instant replay plays, every touch is the tap that skips it: none may draw or swipe underneath (Session 13).</summary>
    public sealed class ReplayTouchBlockerTests
    {
        private static readonly Vec2 AnyTouch = new Vec2(100f, 100f);

        private readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object created in _created) Object.DestroyImmediate(created);
            _created.Clear();
        }

        [Test]
        public void Touches_AreBlocked_OnlyWhileAReplayPlays()
        {
            var views = PerSide<DummyView>.Create(side =>
            {
                var view = Track(new GameObject(side + "View")).AddComponent<DummyView>();
                view.Configure(new Transform[BodyParts.Count], new Transform[BodyParts.Count]);
                return view;
            });
            var playback = new TurnPlayback(views, new ArenaSpace(ArenaSpaceConfig.DefaultWorldUnitsPerArenaUnit));
            var feel = new FeelDirector(Track(ScriptableObject.CreateInstance<FeelConfig>()));
            playback.Pacer = feel;
            var replay = new InstantReplay(playback, feel, null);
            var blocker = new ReplayTouchBlocker(replay);
            Assert.That(blocker.Blocks(AnyTouch), Is.False);

            HitLandedEvent hit = PlayedTurn.Hit(Side.Right, 0.6, 5f);
            replay.Begin(PlayedTurn.Create(GddStartingValues.CreateRulesSettings(), 0, hit),
                new LegendaryMoment(MomentKind.Crush, hit.Time, Side.Left, Vec2.Zero, "test"), new ReplaySettings());
            Assert.That(blocker.Blocks(AnyTouch), Is.True, "the tap skips the replay; it never draws");

            replay.Skip();
            Assert.That(blocker.Blocks(AnyTouch), Is.False);
        }

        private T Track<T>(T created) where T : Object
        {
            _created.Add(created);
            return created;
        }
    }
}
