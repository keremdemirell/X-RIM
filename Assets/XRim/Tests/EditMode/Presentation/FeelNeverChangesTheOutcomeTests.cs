using System.Collections.Generic;
using NUnit.Framework;
using SerializedObject = UnityEditor.SerializedObject;
using UnityEngine;
using XRim.Config;
using XRim.Core;
using XRim.Presentation.Cameras;
using XRim.Presentation.Dummy;
using XRim.Presentation.Feel;
using XRim.Presentation.Playback;
using XRim.Presentation.Vfx;
using XRim.Rules;
using XRim.Simulation;
using XRim.Simulation.Recording;
using XRim.Tests.EditMode.Simulation;

namespace XRim.Tests.EditMode.Presentation
{
    /// <summary>
    /// Feel never changes outcomes (ARCHITECTURE §1, Session 13), the Unity half: a turn rich in big moments (a counter-hit and a death)
    /// is played through every presentation effect (the feel layer's hitstops and slow motion, the camera's framing, shake and punch-in,
    /// the debris) under two very different Feel assets, and nothing in its result changes; simulating the same inputs again still gives
    /// exactly the same result. The engine-free half is <c>TurnOutcomeIndependenceTests</c>.
    /// </summary>
    public sealed class FeelNeverChangesTheOutcomeTests
    {
        private const float FrameSeconds = 1f / 60f;
        private const int MaxFrames = 2000;

        private readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object created in _created) Object.DestroyImmediate(created);
            _created.Clear();
        }

        [Test]
        public void PlayingATurnThroughEveryEffect_ChangesNothingInIt_WhateverTheFeel()
        {
            TurnResult turn = ScriptedDuelTurn.Simulate();
            string before = TurnFingerprint.Of(turn);

            FeelConfig gentle = Track(ScriptableObject.CreateInstance<FeelConfig>());
            FeelConfig extreme = Track(ScriptableObject.CreateInstance<FeelConfig>());
            Set(extreme, "_hitStopBaseSeconds", 0.4f);
            Set(extreme, "_killHitStopSeconds", 0.6f);
            Set(extreme, "_knockOutSlowMotionSpeed", 0.05f);
            Set(extreme, "_counterHitSlowMotionSpeed", 0.05f);
            Set(extreme, "_bigPunchInViewFraction", 0.4f);
            Set(extreme, "_maxShakeViewFraction", 0.2f);

            int gentleFrames = PlayThroughEveryEffect(turn, gentle);
            Assert.That(TurnFingerprint.Of(turn), Is.EqualTo(before), "the default feel changed nothing");

            int extremeFrames = PlayThroughEveryEffect(turn, extreme);
            Assert.That(TurnFingerprint.Of(turn), Is.EqualTo(before), "an extreme feel changed nothing");
            Assert.That(extremeFrames, Is.GreaterThan(gentleFrames), "the extreme feel really did stretch playback");

            Assert.That(TurnFingerprint.Of(ScriptedDuelTurn.Simulate()), Is.EqualTo(before), "the same inputs still give the same turn");
        }

        /// <summary>Plays the turn to its end with every effect attached; returns how many frames it took on screen.</summary>
        private int PlayThroughEveryEffect(TurnResult turn, FeelConfig feel)
        {
            var space = new ArenaSpace(ArenaSpaceConfig.DefaultWorldUnitsPerArenaUnit);
            Transform arenaRoot = Track(new GameObject("ArenaRoot")).transform;
            var views = PerSide<DummyView>.Create(side =>
            {
                var view = Track(new GameObject(side + "View")).AddComponent<DummyView>();
                view.transform.SetParent(arenaRoot, false);
                view.Configure(new Transform[BodyParts.Count], new Transform[BodyParts.Count]);
                return view;
            });
            var playback = new TurnPlayback(views, space);

            var cameraObject = Track(new GameObject("Camera"));
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5.75f;
            var director = cameraObject.AddComponent<FixedSideCameraDirector>();
            director.Configure(feel, space, playback);

            var vfx = Track(new GameObject("Vfx")).AddComponent<MatchVfx>();
            float gravity = new SimulationSettings().GravityUnitsPerSecondSquared;
            vfx.Configure(playback, arenaRoot, space, feel, () => gravity);

            var pacing = new FeelDirector(feel, director);
            playback.Pacer = pacing;
            playback.AddReactor(pacing);
            playback.AddReactor(director);
            playback.AddReactor(vfx);

            playback.Play(turn);
            int frames = 0;
            while (!playback.Player.IsFinished && frames < MaxFrames)
            {
                playback.Advance(FrameSeconds);
                frames++;
            }

            Assert.That(playback.Player.IsFinished, Is.True, "the turn played to its end");
            return frames;
        }

        private T Track<T>(T created) where T : Object
        {
            _created.Add(created);
            return created;
        }

        private static void Set(FeelConfig feel, string field, float value)
        {
            var serialized = new SerializedObject(feel);
            serialized.FindProperty(field).floatValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
