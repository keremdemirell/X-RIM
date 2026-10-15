using System.Collections.Generic;
using UnityEngine;
using XRim.Config;
using XRim.Core;
using XRim.Presentation.Playback;
using XRim.Rules;
using XRim.Rules.Arena;
using XRim.Rules.Events;
using XRim.Simulation.Physics;
using XRim.Simulation.Recording;

namespace XRim.Presentation.Cameras
{
    /// <summary>
    /// The default camera (GDD §1, §2: a fixed side view with one fighter at each end of the screen; assumed, TBD). It never pans, tilts or
    /// follows the action, so distances read the same while bodies move. Session 13 (designer 2026-10-11), each switchable in the Feel
    /// asset:
    /// <list type="bullet">
    /// <item><b>Framing</b> (<see cref="CameraFramingMode"/>): the scene's whole-arena view, or the closer duel framing
    /// (<see cref="DuelFraming"/>), set from each turn's first pose and settling on the next one's after playback, so it never moves while
    /// a player plans (touches map through it).</item>
    /// <item><b>Shake</b> on impacts and a <b>punch-in</b> toward big moments (<see cref="CameraFeelSettings"/>), in real time, so a
    /// hitstop still shakes. Playback holds a turn until the camera is still again (<see cref="IsSettling"/>).</item>
    /// </list>
    /// Framings are kept in the canonical arena and placed through the arena root, so the view flip (GDD §4) mirrors them too. Display
    /// only: it never changes a turn.
    /// </summary>
    public sealed class FixedSideCameraDirector : MonoBehaviour, ICameraDirector
    {
        private const float MirroredScaleX = -1f;
        private const float NormalScaleX = 1f;

        /// <summary>The aspect a framing assumes while the camera reports none (a window not drawn yet).</summary>
        private const float FallbackAspect = 16f / 9f;

        private const float FullHeightPerOrthographicSize = 2f;

        [Tooltip("Parent of every arena visual. Flipping its X scale mirrors the view without touching the simulation.")]
        [SerializeField] private Transform _arenaRoot;

        private readonly CameraShake _shake = new CameraShake();
        private readonly CameraPunch _punch = new CameraPunch();
        private Camera _camera;
        private FeelConfig _feel;
        private ArenaSpace _space;
        private TurnPlayback _playback;
        private ArenaEdges? _edges;
        private CameraFeelSettings _settings;
        private ViewFraming _baseFraming;
        private bool _configured;
        private ViewFraming _current;
        private ViewFraming _glideFrom;
        private ViewFraming _glideTo;
        private float _glideElapsedSeconds;
        private bool _gliding;
        private TurnResult _turn;
        private ViewFraming _endFraming;
        private bool _endFramed = true;
        private bool _focused;
        private Vec2 _focusUnits;
        private float _focusViewFraction = 1f;
        private float _focusSeconds;
        private float _focusElapsedSeconds;

        /// <summary>The framing shown now, before shake and punch-in (debug tools read it).</summary>
        public ViewFraming CurrentFraming => _current;

        public bool IsSettling => _gliding || _shake.IsActive || _punch.IsActive ||
                                  (_turn != null && !_endFramed && !_current.IsCloseTo(_endFraming));

        /// <summary>
        /// Starts directing: the camera's view as the scene set it is the whole-arena framing. Without this call the director only flips
        /// the view (the sandbox before Session 13, tests).
        /// </summary>
        public void Configure(FeelConfig feel, ArenaSpace space, TurnPlayback playback)
        {
            _feel = Guard.NotNull(feel, nameof(feel));
            _space = space;
            _playback = playback;
            _camera = GetComponent<Camera>();
            if (_camera == null || !_camera.orthographic)
            {
                Debug.LogWarning("[XRim] The camera director needs an orthographic camera on its object; framing, shake and punch-in are off.", this);
                return;
            }

            Vector3 local = ToArenaRootLocal(_camera.transform.position);
            _baseFraming = new ViewFraming(_space.ToArena(new Vector2(local.x, local.y)),
                _space.ToArenaLength(_camera.orthographicSize * FullHeightPerOrthographicSize));
            _current = _baseFraming;
            _configured = true;
            RefreshSettings();
        }

        /// <summary>Wires a camera built in code (tests, tools), as XRim/Setup/Create Match Scene does in the Editor.</summary>
        public void SetArenaRoot(Transform arenaRoot) => _arenaRoot = arenaRoot;

        public void SetArenaFlipped(bool flipped)
        {
            if (_arenaRoot == null) return;
            Vector3 scale = _arenaRoot.localScale;
            scale.x = flipped ? MirroredScaleX : NormalScaleX;
            _arenaRoot.localScale = scale;
            Apply();
        }

        public void SetArena(ArenaEdges edges) => _edges = edges;

        public void ShowBoard(PoseSnapshot pose)
        {
            if (!_configured) return;
            RefreshSettings();
            _turn = null;
            _endFramed = true;
            _gliding = false;
            _focused = false;
            _shake.Clear();
            _punch.Clear();
            _current = FramingFor(pose);
            Apply();
        }

        public void BeginTurn(TurnResult turn)
        {
            if (!_configured) return;
            Guard.NotNull(turn, nameof(turn));
            RefreshSettings();
            _turn = turn;
            _gliding = false;
            _focused = false;
            _shake.Clear();
            _punch.Clear();
            IReadOnlyList<TimelineFrame> frames = turn.Timeline.Frames;
            if (frames.Count > 0)
            {
                _current = FramingFor(frames[0].Pose);
                _endFraming = FramingFor(frames[frames.Count - 1].Pose);
            }
            else
            {
                _endFraming = _current;
            }

            _endFramed = false;
            Apply();
        }

        public void FocusOn(Vec2 pointUnits, float viewFraction, float seconds)
        {
            if (!_configured || viewFraction <= 0f || viewFraction >= 1f) return;
            _focused = true;
            _focusUnits = pointUnits;
            _focusViewFraction = viewFraction;
            _focusSeconds = seconds > 0f ? seconds : 0f;
            _focusElapsedSeconds = 0f;
            Apply();
        }

        /// <summary>Impacts shake the camera and big moments punch in toward where they happened (GDD §2, flagged).</summary>
        public void OnMatchEvent(MatchEvent matchEvent)
        {
            if (!_configured || _settings == null || _turn == null || matchEvent == null) return;
            float shake = _settings.ShakeFor(matchEvent);
            if (shake > 0f) _shake.Trigger(shake, _settings.ShakeSeconds, _settings.ShakeFrequencyHz, Seed(matchEvent));

            float punch = _settings.PunchFor(matchEvent);
            if (punch < 1f && MatchEventLocator.TryLocate(_turn, matchEvent, out Vec2 focus))
            {
                _punch.Trigger(focus, punch, _settings.PunchInSeconds, _settings.PunchHoldSeconds, _settings.PunchOutSeconds);
            }
        }

        private void LateUpdate()
        {
            if (!_configured) return;
            float delta = Time.unscaledDeltaTime;
            SettleWhenTheTurnEnds();
            if (_gliding)
            {
                _glideElapsedSeconds += delta;
                float glide = _settings != null ? _settings.FramingGlideSeconds : 0f;
                float t = glide > 0f ? XMath.Clamp01(_glideElapsedSeconds / glide) : 1f;
                _current = _glideFrom.Lerp(_glideTo, t * t * (3f - 2f * t));
                if (t >= 1f)
                {
                    _current = _glideTo;
                    _gliding = false;
                }
            }

            if (_focused) _focusElapsedSeconds += delta;
            _shake.Advance(delta);
            _punch.Advance(delta);
            Apply();
        }

        /// <summary>Once playback reaches the end of the turn, glide to the framing the next planning phase will show.</summary>
        private void SettleWhenTheTurnEnds()
        {
            if (_turn == null || _endFramed || _playback == null) return;
            if (_playback.Current != _turn || _playback.Player.Loop || !_playback.Player.IsFinished) return;
            _endFramed = true;
            IReadOnlyList<TimelineFrame> frames = _turn.Timeline.Frames;
            if (frames.Count > 0) _endFraming = FramingFor(frames[frames.Count - 1].Pose);
            GlideTo(_endFraming);
        }

        private void GlideTo(ViewFraming target)
        {
            float glide = _settings != null ? _settings.FramingGlideSeconds : 0f;
            if (glide <= 0f || _current.IsCloseTo(target))
            {
                _current = target;
                _gliding = false;
                return;
            }

            _glideFrom = _current;
            _glideTo = target;
            _glideElapsedSeconds = 0f;
            _gliding = true;
        }

        private ViewFraming FramingFor(PoseSnapshot pose)
        {
            if (_settings == null || _settings.Framing != CameraFramingMode.Duel || pose == null) return _baseFraming;
            float aspect = _camera.aspect > 0f ? _camera.aspect : FallbackAspect;
            return DuelFraming.Compute(pose.Left.Get(BodyPart.Torso).PositionUnits.X, pose.Right.Get(BodyPart.Torso).PositionUnits.X, aspect,
                _settings, _edges);
        }

        private void RefreshSettings() => _settings = _feel != null ? CameraFeelSettings.From(_feel, _baseFraming.HeightUnits) : null;

        /// <summary>Places the camera: the framing, zoomed about the punch-in's point, pushed by the shake, mapped through the arena root.</summary>
        private void Apply()
        {
            if (!_configured || _camera == null) return;
            Vec2 centre = _current.CentreUnits;
            float height = _current.HeightUnits;
            if (_focused)
            {
                float t = _focusSeconds > 0f ? XMath.Clamp01(_focusElapsedSeconds / _focusSeconds) : 1f;
                float focusZoom = 1f + (_focusViewFraction - 1f) * t * t * (3f - 2f * t);
                centre = _focusUnits + (centre - _focusUnits) * focusZoom;
                height *= focusZoom;
            }

            float zoom = _punch.ZoomFraction;
            if (zoom < 1f)
            {
                centre = _punch.FocusUnits + (centre - _punch.FocusUnits) * zoom;
                height *= zoom;
            }

            if (_settings != null) centre += _shake.Offset(_settings.MaxShakeViewFraction) * height;

            Vector2 local = _space.ToWorld(centre);
            Vector3 world = _arenaRoot != null ? _arenaRoot.TransformPoint(new Vector3(local.x, local.y, 0f)) : new Vector3(local.x, local.y, 0f);
            Transform cameraTransform = _camera.transform;
            cameraTransform.position = new Vector3(world.x, world.y, cameraTransform.position.z);
            _camera.orthographicSize = _space.ToWorldLength(height) / FullHeightPerOrthographicSize;
        }

        private Vector3 ToArenaRootLocal(Vector3 world) => _arenaRoot != null ? _arenaRoot.InverseTransformPoint(world) : world;

        /// <summary>The same event shakes the same way every time it is played (a replay).</summary>
        private static uint Seed(MatchEvent matchEvent) => unchecked((uint)matchEvent.Time.Microseconds * 2654435761u + 1u);

        /// <summary>Serialized field names, for Editor tools that assign references through SerializedObject.</summary>
        public static class FieldNames
        {
            public const string ArenaRoot = nameof(_arenaRoot);
        }
    }
}
