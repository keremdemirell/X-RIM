using System.Collections.Generic;
using NUnit.Framework;
using SerializedObject = UnityEditor.SerializedObject;
using UnityEngine;
using XRim.Config;
using XRim.Core;
using XRim.Presentation.Cameras;
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
    /// <summary>
    /// The default camera (Session 13; GDD §2 camera behaviour TBD, flagged): the duel framing is applied at once when a board is shown, so
    /// the camera never moves while a player plans; the whole-arena framing keeps the scene's view; a flipped view mirrors the framing; a
    /// turn is held while the camera has yet to settle on the next framing or still shakes.
    /// </summary>
    public sealed class FixedSideCameraDirectorTests
    {
        private const float Tolerance = 1e-3f;
        private const float SceneOrthographicSize = 5.75f;
        private const float SceneCameraHeight = 5.25f;

        private readonly List<Object> _created = new List<Object>();
        private ArenaSpace _space;
        private Transform _arenaRoot;
        private Camera _camera;
        private FixedSideCameraDirector _director;
        private FeelConfig _feel;
        private TurnPlayback _playback;

        [SetUp]
        public void SetUp()
        {
            _space = new ArenaSpace(ArenaSpaceConfig.DefaultWorldUnitsPerArenaUnit);
            _arenaRoot = Track(new GameObject("ArenaRoot")).transform;
            var cameraObject = Track(new GameObject("Camera"));
            _camera = cameraObject.AddComponent<Camera>();
            _camera.orthographic = true;
            _camera.orthographicSize = SceneOrthographicSize;
            _camera.aspect = 2f;
            cameraObject.transform.position = new Vector3(0f, SceneCameraHeight, -10f);
            _director = cameraObject.AddComponent<FixedSideCameraDirector>();
            var serialized = new SerializedObject(_director);
            serialized.FindProperty(FixedSideCameraDirector.FieldNames.ArenaRoot).objectReferenceValue = _arenaRoot;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            _feel = Track(ScriptableObject.CreateInstance<FeelConfig>());
            var views = PerSide<DummyView>.Create(side =>
            {
                var view = Track(new GameObject(side + "View")).AddComponent<DummyView>();
                view.Configure(new Transform[BodyParts.Count], new Transform[BodyParts.Count]);
                return view;
            });
            _playback = new TurnPlayback(views, _space);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object created in _created) Object.DestroyImmediate(created);
            _created.Clear();
        }

        [Test]
        public void ShowingABoard_FramesTheDuelAtOnce()
        {
            _director.Configure(_feel, _space, _playback);

            _director.ShowBoard(Pose(-350f, 350f));

            float height = _feel.DuelViewHeightUnits;
            Assert.That(_camera.orthographicSize, Is.EqualTo(_space.ToWorldLength(height) * 0.5f).Within(Tolerance));
            Assert.That(_camera.transform.position.x, Is.EqualTo(0f).Within(Tolerance));
            Assert.That(_camera.transform.position.y, Is.EqualTo(_space.ToWorldLength(height * 0.5f - _feel.FramingFloorMarginUnits)).Within(Tolerance));
            Assert.That(_camera.transform.position.z, Is.EqualTo(-10f), "the camera keeps its depth");
        }

        [Test]
        public void TheWholeArenaFraming_KeepsTheScenesView()
        {
            SetFraming(CameraFramingMode.WholeArena);
            _director.Configure(_feel, _space, _playback);

            _director.ShowBoard(Pose(100f, 500f));

            Assert.That(_camera.orthographicSize, Is.EqualTo(SceneOrthographicSize).Within(Tolerance));
            Assert.That(_camera.transform.position.x, Is.EqualTo(0f).Within(Tolerance));
            Assert.That(_camera.transform.position.y, Is.EqualTo(SceneCameraHeight).Within(Tolerance));
        }

        [Test]
        public void AFlippedView_MirrorsTheFraming()
        {
            _director.Configure(_feel, _space, _playback);
            _director.ShowBoard(Pose(100f, 500f));
            float shownX = _camera.transform.position.x;

            _director.SetArenaFlipped(true);

            Assert.That(shownX, Is.EqualTo(_space.ToWorldLength(300f)).Within(Tolerance), "centred between the fighters");
            Assert.That(_camera.transform.position.x, Is.EqualTo(-shownX).Within(Tolerance));
        }

        [Test]
        public void ATurnThatEndsElsewhere_IsHeld_UntilTheCameraHasSettledOnTheNextFraming()
        {
            _director.Configure(_feel, _space, _playback);

            _director.BeginTurn(Turn(Pose(-350f, 350f), Pose(-200f, 350f)));

            Assert.That(_director.IsSettling, Is.True);
        }

        [Test]
        public void ATurnThatEndsWhereItStarted_IsNotHeld()
        {
            _director.Configure(_feel, _space, _playback);

            _director.BeginTurn(Turn(Pose(-350f, 350f), Pose(-350f, 350f)));

            Assert.That(_director.IsSettling, Is.False);
        }

        [Test]
        public void AnImpact_ShakesTheCamera_AndHoldsTheTurnWhileItShakes()
        {
            _director.Configure(_feel, _space, _playback);
            TurnResult turn = Turn(Pose(-350f, 350f), Pose(-350f, 350f));
            _director.BeginTurn(turn);

            _director.OnMatchEvent(new FighterDiedEvent(SimTime.FromSeconds(0.3), Side.Right));

            Assert.That(_director.IsSettling, Is.True);
        }

        [Test]
        public void ShowingABoard_StopsEveryMovement()
        {
            _director.Configure(_feel, _space, _playback);
            _director.BeginTurn(Turn(Pose(-350f, 350f), Pose(-200f, 350f)));
            _director.OnMatchEvent(new FighterDiedEvent(SimTime.FromSeconds(0.3), Side.Right));

            _director.ShowBoard(Pose(-200f, 350f));

            Assert.That(_director.IsSettling, Is.False);
        }

        private T Track<T>(T created) where T : Object
        {
            _created.Add(created);
            return created;
        }

        private void SetFraming(CameraFramingMode mode)
        {
            var serialized = new SerializedObject(_feel);
            serialized.FindProperty("_cameraFraming").enumValueIndex = (int)mode;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static PoseSnapshot Pose(float leftPelvisX, float rightPelvisX)
        {
            var pose = new PoseSnapshot();
            pose.Left.Set(BodyPart.Torso, new BodyPose(new Vec2(leftPelvisX, 180f), 0f));
            pose.Right.Set(BodyPart.Torso, new BodyPose(new Vec2(rightPelvisX, 180f), 0f));
            return pose;
        }

        private static TurnResult Turn(PoseSnapshot first, PoseSnapshot last)
        {
            RulesSettings settings = GddStartingValues.CreateRulesSettings();
            BoardSnapshot board = TestData.CreateInitialBoard(settings);
            var recorder = new TimelineRecorder();
            recorder.RecordFrame(0, SimTime.Zero, first);
            recorder.RecordFrame(1, SimTime.FromSeconds(1.0), last);
            return new TurnResult(0, recorder.Build(), board, new ExecutionReport(board.State, new PerSide<SimTime?>(null, null), false));
        }
    }
}
