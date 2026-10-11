using System;
using System.Collections.Generic;
using UnityEngine;
using XRim.Config;
using XRim.Core;
using XRim.Presentation.Dummy;
using XRim.Presentation.Playback;
using XRim.Rules;
using XRim.Rules.Events;
using XRim.Simulation.Physics;
using XRim.Simulation.Recording;

namespace XRim.Presentation.Vfx
{
    /// <summary>
    /// Bloodless mechanical destruction (GDD §2, Decided): when a limb is severed it sprays bolts, nuts and hydraulic fluid from the joint
    /// (§12), and a dummy whose torso splits sprays from the torso's middle (§11). Placeholder shapes and numbers (<see cref="FeelConfig"/>);
    /// Session 13 polishes them. Debris is cosmetic: it lives in the visual scene, never feeds back into the simulation, and follows the
    /// playback clock (<see cref="DebrisFlight"/>), so slow motion, a hitstop and scrubbing all apply. Each spray is seeded from its event,
    /// so a replay shows the same one. The pieces stay on the floor until the next turn plays.
    /// </summary>
    public sealed class MatchVfx : MonoBehaviour, IMatchEventReactor
    {
        private const string PieceName = "Debris";

        /// <summary>Above the dummies and their held items.</summary>
        private const int SortingOrder = 40;

        /// <summary>The spray leaves upward within this angle of straight up, either side.</summary>
        private const float SprayHalfAngleDegrees = 70f;

        /// <summary>Each piece leaves at between this share of the spray speed and all of it.</summary>
        private const float MinSpeedShare = 0.4f;

        private const float MaxSpinDegreesPerSecond = 900f;

        /// <summary>A nut is drawn bigger than a bolt by this much.</summary>
        private const float NutSizeMultiplier = 1.3f;

        /// <summary>More pieces than this at once and the oldest are cleared.</summary>
        private const int MaxPieces = 200;

        private static readonly Color BoltColor = new Color(0.62f, 0.63f, 0.66f);
        private static readonly Color NutColor = new Color(0.36f, 0.36f, 0.39f);
        private static readonly Color FluidColor = new Color(0.25f, 0.6f, 0.95f, 0.9f);

        private readonly List<Piece> _pieces = new List<Piece>();
        private TurnPlayback _playback;
        private Transform _arenaRoot;
        private ArenaSpace _space;
        private FeelConfig _feel;
        private Func<float> _gravity;
        private TurnTimeline _timeline;
        private Sprite _sprite;
        private Texture2D _texture;

        /// <param name="gravityUnitsPerSecondSquared">The simulation's gravity, read for each spray (it is tunable).</param>
        public void Configure(TurnPlayback playback, Transform arenaRoot, ArenaSpace space, FeelConfig feel, Func<float> gravityUnitsPerSecondSquared)
        {
            _playback = Guard.NotNull(playback, nameof(playback));
            _arenaRoot = Guard.NotNull(arenaRoot, nameof(arenaRoot));
            _space = space;
            _feel = Guard.NotNull(feel, nameof(feel));
            _gravity = Guard.NotNull(gravityUnitsPerSecondSquared, nameof(gravityUnitsPerSecondSquared));
        }

        public void OnMatchEvent(MatchEvent matchEvent)
        {
            if (_playback == null) return;
            ForgetAnEarlierTurn();
            switch (matchEvent)
            {
                case LimbSeveredEvent severed:
                    Spray(JointOf(severed.Side, severed.Part), severed.Time, Seed(severed.Time, severed.Side, (int)severed.Part));
                    break;
                case FighterDiedEvent died:
                    Spray(CentreOf(died.Side, BodyPart.Torso), died.Time, Seed(died.Time, died.Side, BodyParts.Count));
                    break;
            }
        }

        private void LateUpdate()
        {
            if (_playback == null) return;
            ForgetAnEarlierTurn();
            SimTime now = _playback.Player.CurrentTime;
            foreach (Piece piece in _pieces)
            {
                float seconds = (float)(now - piece.SprayTime).Seconds;
                bool flying = seconds >= 0f;
                if (piece.Transform.gameObject.activeSelf != flying) piece.Transform.gameObject.SetActive(flying);
                if (!flying) continue;
                Vec2 position = piece.Flight.PositionAt(seconds, out float rotation);
                piece.Transform.SetLocalPositionAndRotation(_space.ToWorld(position), Quaternion.Euler(0f, 0f, rotation));
            }
        }

        private void OnDestroy()
        {
            Clear();
            if (_sprite != null) Destroy(_sprite);
            if (_texture != null) Destroy(_texture);
        }

        /// <summary>A new turn plays: last turn's debris goes (it would follow the wrong clock).</summary>
        private void ForgetAnEarlierTurn()
        {
            TurnTimeline timeline = _playback.Player.Timeline;
            if (timeline == _timeline) return;
            _timeline = timeline;
            Clear();
        }

        private void Spray(Vec2 origin, SimTime time, uint seed)
        {
            var random = new XorShiftRandom(seed);
            float gravity = _gravity();
            for (int i = 0; i < _feel.DebrisPieces; i++)
            {
                bool nut = i % 2 == 1;
                float size = _feel.DebrisSizeUnits * (nut ? NutSizeMultiplier : 1f);
                Add(origin, time, random, gravity, size, nut ? NutColor : BoltColor);
            }

            for (int i = 0; i < _feel.FluidDrops; i++)
            {
                Add(origin, time, random, gravity, _feel.FluidDropSizeUnits, FluidColor);
            }

            while (_pieces.Count > MaxPieces)
            {
                Destroy(_pieces[0].Transform.gameObject);
                _pieces.RemoveAt(0);
            }
        }

        private void Add(Vec2 origin, SimTime time, IRandom random, float gravity, float sizeUnits, Color color)
        {
            float angle = 90f + (random.NextFloat() * 2f - 1f) * SprayHalfAngleDegrees;
            float speed = _feel.DebrisSpeedUnitsPerSecond * (MinSpeedShare + random.NextFloat() * (1f - MinSpeedShare));
            float spin = (random.NextFloat() * 2f - 1f) * MaxSpinDegreesPerSecond;
            var flight = new DebrisFlight(origin, Vec2.FromAngleDegrees(angle) * speed, spin, gravity, LegGeometry.FloorYUnits, _feel.DebrisBounceFraction);

            var piece = new GameObject(PieceName);
            piece.transform.SetParent(_arenaRoot, false);
            piece.transform.localScale = new Vector3(_space.ToWorldLength(sizeUnits), _space.ToWorldLength(sizeUnits), 1f);
            var renderer = piece.AddComponent<SpriteRenderer>();
            renderer.sprite = Sprite();
            renderer.color = color;
            renderer.sortingOrder = SortingOrder;
            piece.SetActive(false);
            _pieces.Add(new Piece(piece.transform, flight, time));
        }

        /// <summary>Where a limb was jointed to its dummy: its drawn pivot, in arena units.</summary>
        private Vec2 JointOf(Side side, BodyPart part) => ToArena(_playback.Views[side].PartPosition(part));

        private Vec2 CentreOf(Side side, BodyPart part) => ToArena(_playback.Views[side].PartCentre(part));

        private Vec2 ToArena(Vector3 world)
        {
            Vector3 local = _arenaRoot.InverseTransformPoint(world);
            return _space.ToArena(new Vector2(local.x, local.y));
        }

        private static uint Seed(SimTime time, Side side, int what) =>
            unchecked((uint)time.Microseconds * 31u + (uint)side * 7u + (uint)what + 1u);

        /// <summary>One white square, scaled and tinted per piece.</summary>
        private Sprite Sprite()
        {
            if (_sprite != null) return _sprite;
            _texture = new Texture2D(1, 1);
            _texture.SetPixel(0, 0, Color.white);
            _texture.Apply();
            _sprite = UnityEngine.Sprite.Create(_texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
            return _sprite;
        }

        private void Clear()
        {
            foreach (Piece piece in _pieces)
            {
                if (piece.Transform != null) Destroy(piece.Transform.gameObject);
            }

            _pieces.Clear();
        }

        private readonly struct Piece
        {
            public Transform Transform { get; }
            public DebrisFlight Flight { get; }
            public SimTime SprayTime { get; }

            public Piece(Transform transform, DebrisFlight flight, SimTime sprayTime)
            {
                Transform = transform;
                Flight = flight;
                SprayTime = sprayTime;
            }
        }
    }
}
