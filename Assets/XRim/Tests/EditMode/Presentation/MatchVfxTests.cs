using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using XRim.Config;
using XRim.Core;
using XRim.Presentation.Dummy;
using XRim.Presentation.Playback;
using XRim.Presentation.Vfx;
using XRim.Rules;
using XRim.Rules.Events;
using XRim.Rules.Settings;
using XRim.Rules.Status;
using XRim.Simulation;
using XRim.Simulation.Recording;
using SerializedObject = UnityEditor.SerializedObject;

namespace XRim.Tests.EditMode.Presentation
{
    /// <summary>
    /// The mechanical effects in play (Session 13): the pool never holds more pieces than the Feel asset's cap, a replay of the same turn
    /// draws each effect once, and a new turn starts clean.
    /// </summary>
    public sealed class MatchVfxTests
    {
        private const int Cap = 20;
        private const float FrameSeconds = 1f / 60f;

        private readonly List<Object> _created = new List<Object>();
        private TurnPlayback _playback;
        private MatchVfx _vfx;

        [SetUp]
        public void SetUp()
        {
            var space = new ArenaSpace(ArenaSpaceConfig.DefaultWorldUnitsPerArenaUnit);
            Transform arenaRoot = Track(new GameObject("ArenaRoot")).transform;
            var views = PerSide<DummyView>.Create(side =>
            {
                var view = Track(new GameObject(side + "View")).AddComponent<DummyView>();
                view.Configure(new Transform[BodyParts.Count], new Transform[BodyParts.Count]);
                return view;
            });
            _playback = new TurnPlayback(views, space);

            FeelConfig feel = Track(ScriptableObject.CreateInstance<FeelConfig>());
            var serialized = new SerializedObject(feel);
            serialized.FindProperty("_maxVfxPieces").intValue = Cap;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            _vfx = Track(new GameObject("Vfx")).AddComponent<MatchVfx>();
            float gravity = new SimulationSettings().GravityUnitsPerSecondSquared;
            _vfx.Configure(_playback, arenaRoot, space, feel, () => gravity);
            _playback.AddReactor(_vfx);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object created in _created) Object.DestroyImmediate(created);
            _created.Clear();
        }

        [Test]
        public void ManyEffects_NeverHoldMoreThanTheCap()
        {
            PlayToTheEnd(BigTurn());

            Assert.That(_vfx.PieceCapacity, Is.EqualTo(Cap));
            Assert.That(_vfx.ActivePieces, Is.EqualTo(Cap), "a sever, a death and hits spray far more than 20: the oldest are reused");
        }

        [Test]
        public void AReplay_DrawsEachEffectOnce()
        {
            PlayToTheEnd(SmallTurn());
            int first = _vfx.ActivePieces;
            Assert.That(first, Is.GreaterThan(0).And.LessThan(Cap), "test setup: the turn's effects fit in the pool");

            _playback.Replay();
            AdvanceToTheEnd();

            Assert.That(_vfx.ActivePieces, Is.EqualTo(first));
        }

        [Test]
        public void ANewTurn_StartsClean()
        {
            PlayToTheEnd(SmallTurn());

            // In play the next frame clears; here (no frames run in EditMode) the new turn's first event does.
            PlayToTheEnd(PlayedTurn.Create(GddStartingValues.CreateRulesSettings(), 1,
                new StatusAppliedEvent(SimTime.FromSeconds(0.1), Side.Left, StatusKind.Stunned)));

            Assert.That(_vfx.ActivePieces, Is.EqualTo(0));
        }

        private void PlayToTheEnd(TurnResult turn)
        {
            _playback.Play(turn);
            AdvanceToTheEnd();
        }

        private void AdvanceToTheEnd()
        {
            for (int frame = 0; frame < 200 && !_playback.Player.IsFinished; frame++) _playback.Advance(FrameSeconds);
        }

        /// <summary>One light hit: its sparks and flash.</summary>
        private static TurnResult SmallTurn() => PlayedTurn.Create(GddStartingValues.CreateRulesSettings(), 0, PlayedTurn.Hit(Side.Right, 0.3, 4f));

        private static TurnResult BigTurn() => PlayedTurn.Create(GddStartingValues.CreateRulesSettings(), 0,
            PlayedTurn.Hit(Side.Right, 0.2, 30f), new LimbSeveredEvent(SimTime.FromSeconds(0.25), Side.Right, BodyPart.RightArm),
            new FighterDiedEvent(SimTime.FromSeconds(0.4), Side.Right));

        private T Track<T>(T created) where T : Object
        {
            _created.Add(created);
            return created;
        }
    }
}
