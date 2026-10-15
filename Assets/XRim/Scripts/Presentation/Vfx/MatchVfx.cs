using System;
using System.Collections.Generic;
using UnityEngine;
using XRim.Config;
using XRim.Core;
using XRim.Presentation.Arena;
using XRim.Presentation.Dummy;
using XRim.Presentation.Playback;
using XRim.Rules;
using XRim.Rules.Events;
using XRim.Rules.Settings;
using XRim.Simulation.Physics;
using XRim.Simulation.Recording;

namespace XRim.Presentation.Vfx
{
    /// <summary>
    /// Bloodless mechanical destruction and impact effects (GDD §2, Decided: bolts, nuts, springs, sparks and hydraulic fluid; no blood;
    /// Session 13). For every played event it draws what <see cref="VfxRecipes"/> says (sparks off hits, a shower on a rebound, a thud on
    /// a crush, rings on blocks, a burst and a fluid jet from a severed joint, a bigger burst and steam from a splitting torso, arcs on a
    /// wall zap, dust under body moves); a struck part flashes and the struck dummy's drawing flinches; each swinging blade leaves a
    /// fading trail. Cosmetic: it lives in the visual scene and never feeds back into the simulation. Everything follows the playback clock
    /// (pure functions of time: <see cref="DebrisFlight"/>, <see cref="SparkFlight"/>), so slow motion, a hitstop and scrubbing apply,
    /// and each effect is seeded from its event, so a replay shows the same one. Pieces come from a pool capped by the Feel asset, the
    /// oldest reused first; debris stays on the floor until the next turn plays. Colours come from each side's <see cref="VfxStyle"/> (a
    /// cosmetic hit-effect set, §16).
    /// </summary>
    public sealed class MatchVfx : MonoBehaviour, IMatchEventReactor
    {
        private const string PieceName = "Vfx";
        private const string ArcName = "VfxArc";
        private const string TrailName = "VfxTrail";

        // Draw order: dummies use -20..30 (far limbs, torso, head, near limbs, held item).
        private const int TrailSortingOrder = 25;
        private const int DebrisSortingOrder = 40;
        private const int SparkSortingOrder = 45;
        private const int GlowSortingOrder = 50;

        /// <summary>Each piece leaves at between this share of its burst's speed and all of it.</summary>
        private const float MinSpeedShare = 0.4f;

        private const float MaxSpinDegreesPerSecond = 900f;

        /// <summary>A spark streak is this many times its size long at full speed, never shorter than its size.</summary>
        internal const float SparkStretch = 3.5f;

        /// <summary>A spark streak is this share of its size thick.</summary>
        internal const float SparkThicknessShare = 0.5f;

        /// <summary>A flash grows by this share of its size as it fades.</summary>
        internal const float FlashGrowth = 0.5f;

        /// <summary>A drop of fluid lies flattened on the floor once it landed.</summary>
        internal const float PuddleWidthMultiplier = 1.7f;

        internal const float PuddleHeightMultiplier = 0.4f;

        /// <summary>A puff grows to this many times its size as it fades.</summary>
        internal const float PuffGrowth = 1.5f;

        /// <summary>A ring starts at this share of its size and grows to all of it.</summary>
        internal const float RingStartShare = 0.3f;

        private const int ArcPoints = 12;
        private const int MaxArcs = 12;

        /// <summary>An arc is redrawn with a new jag this often, in seconds of the turn: the crackle.</summary>
        private const float ArcRejagSeconds = 0.03f;

        private const float ArcWidthUnits = 4f;
        private const int SideCount = 2;

        private readonly List<VfxBurst> _bursts = new List<VfxBurst>();
        private readonly List<VfxArc> _arcSpecs = new List<VfxArc>();
        private readonly List<Vec2> _trailPoints = new List<Vec2>();
        private readonly List<ActiveArc> _arcs = new List<ActiveArc>();
        private readonly List<StruckPart> _flashes = new List<StruckPart>();
        private readonly List<Flinch> _flinches = new List<Flinch>();
        private readonly VfxStyle[] _styles = { new VfxStyle(), new VfxStyle() };
        private readonly VfxStyle _sharedStyle = new VfxStyle();
        private readonly Vec2[] _arcScratch = new Vec2[ArcPoints];
        private readonly int[] _flashedParts = new int[SideCount];

        private TurnPlayback _playback;
        private Transform _arenaRoot;
        private ArenaSpace _space;
        private FeelConfig _feel;
        private Func<float> _gravity;
        private Func<WeaponId, WeaponStats> _weapons;
        private VfxSprites _sprites;
        private Material _lineMaterial;
        private VfxSettings _settings;
        private VfxSlots _slots;
        private Piece[] _pieces = Array.Empty<Piece>();
        private LineRenderer[] _arcLines = Array.Empty<LineRenderer>();
        private readonly LineRenderer[] _trails = new LineRenderer[SideCount];
        private TurnTimeline _timeline;
        private SimTime _lastTime;
        private int _pass;

        /// <summary>How many effect pieces are drawn now (debug tools and tests read it).</summary>
        public int ActivePieces => _slots != null ? _slots.ActiveCount : 0;

        /// <summary>The most pieces at once this turn (the Feel asset's cap).</summary>
        public int PieceCapacity => _slots != null ? _slots.Capacity : 0;

        /// <param name="gravityUnitsPerSecondSquared">The simulation's gravity, read each turn (it is tunable).</param>
        /// <param name="weapons">Each weapon's stats (a trail follows the weapon's tip); null draws no trails.</param>
        public void Configure(TurnPlayback playback, Transform arenaRoot, ArenaSpace space, FeelConfig feel, Func<float> gravityUnitsPerSecondSquared,
            Func<WeaponId, WeaponStats> weapons = null)
        {
            _playback = Guard.NotNull(playback, nameof(playback));
            _arenaRoot = Guard.NotNull(arenaRoot, nameof(arenaRoot));
            _space = space;
            _feel = Guard.NotNull(feel, nameof(feel));
            _gravity = Guard.NotNull(gravityUnitsPerSecondSquared, nameof(gravityUnitsPerSecondSquared));
            _weapons = weapons;
            _sprites = new VfxSprites();
            _lineMaterial = ArenaLines.CreateMaterial();
        }

        /// <summary>The hit-effect set a side's effects are drawn in (a cosmetic, GDD §16); the default set until one is given.</summary>
        public void SetStyle(Side side, VfxStyle style) => _styles[(int)side] = Guard.NotNull(style, nameof(style));

        public void OnMatchEvent(MatchEvent matchEvent)
        {
            if (_playback == null || matchEvent == null) return;
            SyncWithPlayback();
            TurnResult turn = _playback.Current;
            if (turn == null || turn.Timeline != _timeline) return;

            _bursts.Clear();
            _arcSpecs.Clear();
            VfxRecipes.Add(matchEvent, turn, _settings, _bursts, _arcSpecs);
            var random = new XorShiftRandom(Seed(matchEvent, turn));
            foreach (VfxBurst burst in _bursts)
            {
                Spawn(burst, matchEvent.Time, random);
            }

            foreach (VfxArc arc in _arcSpecs)
            {
                AddArc(arc, matchEvent.Time, random.NextInt(1, int.MaxValue));
            }

            if (matchEvent is HitLandedEvent hit) Strike(hit);
        }

        private void LateUpdate()
        {
            if (_playback == null) return;
            SyncWithPlayback();
            if (_timeline == null) return;
            SimTime now = _playback.Player.CurrentTime;
            UpdatePieces(now);
            UpdateArcs(now);
            UpdateTrails(now);
            UpdateStruckDummies(now);
        }

        private void OnDestroy()
        {
            foreach (Piece piece in _pieces)
            {
                if (piece != null && piece.Renderer != null) Destroy(piece.Renderer.gameObject);
            }

            foreach (LineRenderer line in _arcLines)
            {
                if (line != null) Destroy(line.gameObject);
            }

            foreach (LineRenderer line in _trails)
            {
                if (line != null) Destroy(line.gameObject);
            }

            _sprites?.Dispose();
            if (_lineMaterial != null) Destroy(_lineMaterial);
        }

        // ----- following playback -----

        /// <summary>
        /// A new turn plays: last turn's effects go and this turn's settings are read. The same turn plays again (a replay, a loop): the
        /// effects from where the new pass starts go, since its events fire again. Playback was scrubbed back: the effects that have not
        /// happened yet at this time go (their events fire again as playback passes them).
        /// </summary>
        private void SyncWithPlayback()
        {
            TimelinePlayer player = _playback.Player;
            TurnTimeline timeline = player.Timeline;
            SimTime now = player.CurrentTime;
            if (timeline != _timeline)
            {
                _timeline = timeline;
                _pass = player.PassCount;
                _settings = VfxSettings.From(_feel, _gravity());
                EnsurePool(Math.Max(1, _settings.MaxPieces));
                ClearAll();
            }
            else if (player.PassCount != _pass)
            {
                _pass = player.PassCount;
                ForgetFrom(player.PassStart, true);
            }
            else if (now < _lastTime)
            {
                ForgetFrom(now, false);
            }

            _lastTime = now;
        }

        private void ClearAll()
        {
            _slots?.Clear();
            foreach (Piece piece in _pieces) piece?.Hide();
            foreach (ActiveArc arc in _arcs) _arcLines[arc.Line].positionCount = 0;
            _arcs.Clear();
            _flashes.Clear();
            _flinches.Clear();
            ResetStruckDummies();
        }

        /// <summary>Drops every effect that starts after this time (or at it too, when its events fire again from there).</summary>
        private void ForgetFrom(SimTime time, bool atTimeToo)
        {
            for (int slot = 0; slot < _pieces.Length; slot++)
            {
                Piece piece = _pieces[slot];
                if (piece == null || !_slots.IsUsed(slot) || !IsFrom(piece.Spawn, time, atTimeToo)) continue;
                piece.Hide();
                _slots.Release(slot);
            }

            for (int i = _arcs.Count - 1; i >= 0; i--)
            {
                if (!IsFrom(_arcs[i].Spawn, time, atTimeToo)) continue;
                _arcLines[_arcs[i].Line].positionCount = 0;
                _arcs.RemoveAt(i);
            }

            _flashes.RemoveAll(flash => IsFrom(flash.Spawn, time, atTimeToo));
            _flinches.RemoveAll(flinch => IsFrom(flinch.Spawn, time, atTimeToo));
        }

        private static bool IsFrom(SimTime spawn, SimTime time, bool atTimeToo) => spawn > time || (atTimeToo && spawn == time);

        // ----- pieces -----

        private void EnsurePool(int capacity)
        {
            if (_slots != null && _slots.Capacity == capacity) return;
            foreach (Piece piece in _pieces)
            {
                if (piece != null && piece.Renderer != null) Destroy(piece.Renderer.gameObject);
            }

            _slots = new VfxSlots(capacity);
            _pieces = new Piece[capacity];
        }

        private void Spawn(VfxBurst burst, SimTime time, IRandom random)
        {
            Color color = StyleOf(burst.StyleSide).ColorOf(burst.Color);
            for (int i = 0; i < burst.Count; i++)
            {
                float angle = burst.DirectionDegrees + (random.NextFloat() * 2f - 1f) * burst.SpreadDegrees;
                float speed = burst.SpeedUnitsPerSecond * (MinSpeedShare + random.NextFloat() * (1f - MinSpeedShare));
                Vec2 velocity = Vec2.FromAngleDegrees(angle) * speed;
                float spin = burst.Kind == VfxParticleKind.Drop ? 0f : (random.NextFloat() * 2f - 1f) * MaxSpinDegreesPerSecond;

                int slot = _slots.Acquire();
                Piece piece = _pieces[slot] ?? (_pieces[slot] = CreatePiece());
                piece.Start(burst, time, velocity, spin, color, _settings);
                piece.Renderer.sprite = _sprites.For(burst.Kind);
                piece.Renderer.sortingOrder = SortingOrderOf(burst.Kind);
            }
        }

        private Piece CreatePiece()
        {
            var gameObject = new GameObject(PieceName);
            gameObject.transform.SetParent(_arenaRoot, false);
            var renderer = gameObject.AddComponent<SpriteRenderer>();
            gameObject.SetActive(false);
            return new Piece(renderer);
        }

        private void UpdatePieces(SimTime now)
        {
            for (int slot = 0; slot < _pieces.Length; slot++)
            {
                Piece piece = _pieces[slot];
                if (piece == null || !_slots.IsUsed(slot)) continue;
                float seconds = (float)(now - piece.Spawn).Seconds;
                if (piece.Lifetime > 0f && seconds >= piece.Lifetime)
                {
                    piece.Hide();
                    _slots.Release(slot);
                    continue;
                }

                piece.Show(seconds, _space);
            }
        }

        private static int SortingOrderOf(VfxParticleKind kind)
        {
            switch (kind)
            {
                case VfxParticleKind.Spark: return SparkSortingOrder;
                case VfxParticleKind.Flash:
                case VfxParticleKind.Ring:
                case VfxParticleKind.Puff:
                    return GlowSortingOrder;
                default: return DebrisSortingOrder;
            }
        }

        // ----- arcs -----

        private void AddArc(VfxArc arc, SimTime time, int seed)
        {
            if (_lineMaterial == null) return;
            if (_arcLines.Length == 0)
            {
                _arcLines = new LineRenderer[MaxArcs];
                for (int i = 0; i < MaxArcs; i++)
                {
                    _arcLines[i] = ArenaLines.Create(_arenaRoot, ArcName + i, _lineMaterial, Color.white, _space.ToWorldLength(ArcWidthUnits), GlowSortingOrder);
                }
            }

            if (_arcs.Count >= MaxArcs)
            {
                _arcLines[_arcs[0].Line].positionCount = 0;
                _arcs.RemoveAt(0);
            }

            int line = FreeArcLine();
            _arcLines[line].startColor = _sharedStyle.Zap;
            _arcLines[line].endColor = _sharedStyle.Zap;
            _arcs.Add(new ActiveArc(arc, time, (uint)seed, line));
        }

        private int FreeArcLine()
        {
            for (int line = 0; line < _arcLines.Length; line++)
            {
                bool taken = false;
                foreach (ActiveArc arc in _arcs) taken |= arc.Line == line;
                if (!taken) return line;
            }

            return 0;
        }

        private void UpdateArcs(SimTime now)
        {
            for (int i = _arcs.Count - 1; i >= 0; i--)
            {
                ActiveArc arc = _arcs[i];
                float seconds = (float)(now - arc.Spawn).Seconds;
                LineRenderer line = _arcLines[arc.Line];
                if (seconds >= arc.Spec.LifetimeSeconds)
                {
                    line.positionCount = 0;
                    _arcs.RemoveAt(i);
                    continue;
                }

                if (seconds < 0f)
                {
                    line.positionCount = 0;
                    continue;
                }

                uint jag = arc.Seed + (uint)(seconds / ArcRejagSeconds);
                LightningArc.Fill(arc.Spec.FromUnits, arc.Spec.ToUnits, arc.Spec.JitterUnits, jag, _arcScratch);
                line.positionCount = ArcPoints;
                for (int p = 0; p < ArcPoints; p++)
                {
                    line.SetPosition(p, _space.ToWorld(_arcScratch[p]));
                }

                Color color = _sharedStyle.Zap;
                color.a *= 1f - seconds / arc.Spec.LifetimeSeconds;
                line.startColor = color;
                line.endColor = color;
            }
        }

        // ----- trails -----

        private void UpdateTrails(SimTime now)
        {
            TurnResult turn = _playback.Current;
            for (int side = 0; side < SideCount; side++)
            {
                LineRenderer line = Trail(side);
                if (line == null) continue;
                _trailPoints.Clear();
                WeaponStats weapon = turn != null && _weapons != null && _settings.TrailSeconds > 0f
                    ? _weapons(turn.Report.ResolvedState.Fighters[(Side)side].CurrentWeapon)
                    : null;
                if (weapon != null && LeavesATrail(weapon))
                {
                    WeaponTrail.Collect(_timeline, (Side)side, now, _settings.TrailSeconds, _settings.TrailMinSpeedUnitsPerSecond,
                        HeldItemShape.Of(weapon), _trailPoints);
                }

                line.positionCount = _trailPoints.Count;
                if (_trailPoints.Count == 0) continue;
                for (int p = 0; p < _trailPoints.Count; p++)
                {
                    line.SetPosition(p, _space.ToWorld(_trailPoints[p]));
                }

                VfxStyle style = _styles[side];
                line.startColor = style.TrailTail;
                line.endColor = style.TrailHead;
                line.startWidth = 0f;
                line.endWidth = _space.ToWorldLength(weapon.InkThicknessUnits * _settings.TrailWidthMultiplier);
            }
        }

        private LineRenderer Trail(int side)
        {
            if (_trails[side] != null || _lineMaterial == null) return _trails[side];
            _trails[side] = ArenaLines.Create(_arenaRoot, TrailName + (Side)side, _lineMaterial, Color.white, 1f, TrailSortingOrder);
            _trails[side].widthMultiplier = 1f;
            return _trails[side];
        }

        /// <summary>Swung weapons and clubs leave a trail; a shield and a headbutt do not.</summary>
        private static bool LeavesATrail(WeaponStats weapon) => weapon.Kind == WeaponKind.Weapon || weapon.Kind == WeaponKind.SeveredLimb;

        // ----- the struck dummy -----

        private void Strike(HitLandedEvent hit)
        {
            Side victim = hit.Hit.Victim;
            float damage = hit.Damage.Damage;
            _flashes.Add(new StruckPart(victim, hit.Hit.Part, hit.Time));
            _flinches.Add(new Flinch(victim, hit.Time,
                HitFlinch.AngleDegrees(victim, damage, _settings.FlinchDegreesPerDamage, _settings.MaxFlinchDegrees),
                HitFlinch.PushUnits(victim, damage, _settings.FlinchPushUnitsPerDamage, _settings.MaxFlinchPushUnits)));
        }

        private void UpdateStruckDummies(SimTime now)
        {
            PerSide<DummyView> views = _playback.Views;
            for (int side = 0; side < SideCount; side++)
            {
                DummyView view = views[(Side)side];
                if (view == null) continue;

                float degrees = 0f, push = 0f;
                foreach (Flinch flinch in _flinches)
                {
                    if ((int)flinch.Side != side) continue;
                    float strength = HitFlinch.StrengthAt((float)(now - flinch.Spawn).Seconds, _settings.FlinchSeconds);
                    degrees += flinch.Degrees * strength;
                    push += flinch.PushUnits * strength;
                }

                view.SetFlinch(degrees, new Vec2(push, 0f));

                int flashed = 0;
                Color flashColor = _styles[(int)((Side)side).Opponent()].PartFlash;
                foreach (BodyPart part in BodyParts.All)
                {
                    float amount = FlashAmount((Side)side, part, now);
                    int bit = 1 << (int)part;
                    if (amount > 0f) flashed |= bit;
                    if (amount > 0f || (_flashedParts[side] & bit) != 0) view.SetPartFlash(part, amount, flashColor);
                }

                _flashedParts[side] = flashed;
            }
        }

        private float FlashAmount(Side side, BodyPart part, SimTime now)
        {
            float amount = 0f;
            foreach (StruckPart flash in _flashes)
            {
                if (flash.Side != side || flash.Part != part || _settings.PartFlashSeconds <= 0f) continue;
                float seconds = (float)(now - flash.Spawn).Seconds;
                if (seconds < 0f || seconds >= _settings.PartFlashSeconds) continue;
                amount = Math.Max(amount, 1f - seconds / _settings.PartFlashSeconds);
            }

            return amount;
        }

        private void ResetStruckDummies()
        {
            if (_playback == null) return;
            PerSide<DummyView> views = _playback.Views;
            for (int side = 0; side < SideCount; side++)
            {
                DummyView view = views[(Side)side];
                if (view == null) continue;
                view.SetFlinch(0f, Vec2.Zero);
                foreach (BodyPart part in BodyParts.All)
                {
                    if ((_flashedParts[side] & (1 << (int)part)) != 0) view.SetPartFlash(part, 0f, Color.white);
                }

                _flashedParts[side] = 0;
            }
        }

        private VfxStyle StyleOf(Side? side) => side.HasValue ? _styles[(int)side.Value] : _sharedStyle;

        /// <summary>The same event sprays the same way every time it is played (a replay): seeded from its time and its place in the turn.</summary>
        private static uint Seed(MatchEvent matchEvent, TurnResult turn)
        {
            IReadOnlyList<MatchEvent> events = turn.Timeline.Events;
            int index = 0;
            while (index < events.Count && !ReferenceEquals(events[index], matchEvent)) index++;
            return unchecked((uint)matchEvent.Time.Microseconds * 2654435761u + (uint)index * 40503u + 1u);
        }

        /// <summary>One pooled piece: its drawing and how it moves, a pure function of the time since it flew off.</summary>
        private sealed class Piece
        {
            private DebrisFlight _debris;
            private SparkFlight _spark;
            private Vec2 _origin;
            private Vec2 _drift;
            private float _size;
            private Color _color;
            private float _landingSeconds;

            public Piece(SpriteRenderer renderer)
            {
                Renderer = renderer;
            }

            public SpriteRenderer Renderer { get; }
            public VfxParticleKind Kind { get; private set; }
            public SimTime Spawn { get; private set; }

            /// <summary>Seconds of the turn it lasts; 0 = it stays.</summary>
            public float Lifetime { get; private set; }

            public void Start(VfxBurst burst, SimTime spawn, Vec2 velocity, float spin, Color color, VfxSettings settings)
            {
                Kind = burst.Kind;
                Spawn = spawn;
                Lifetime = burst.LifetimeSeconds;
                _origin = burst.OriginUnits;
                _drift = velocity;
                _size = burst.SizeUnits;
                _color = color;
                float gravity = settings.GravityUnitsPerSecondSquared;
                _debris = new DebrisFlight(_origin, velocity, spin, gravity, LegGeometry.FloorYUnits, settings.DebrisBounceFraction);
                _spark = new SparkFlight(_origin, velocity, gravity, Lifetime);
                _landingSeconds = Kind == VfxParticleKind.Drop ? _debris.FirstLandingSeconds : float.MaxValue;
                Renderer.color = color;
            }

            public void Show(float seconds, ArenaSpace space)
            {
                bool visible = seconds >= 0f;
                if (Renderer.gameObject.activeSelf != visible) Renderer.gameObject.SetActive(visible);
                if (!visible) return;

                Transform transform = Renderer.transform;
                float life = Lifetime > 0f ? XMath.Clamp01(seconds / Lifetime) : 0f;
                switch (Kind)
                {
                    case VfxParticleKind.Spark:
                    {
                        Vec2 velocity = _spark.VelocityAt(seconds);
                        float stretch = Math.Max(1f, Math.Min(SparkStretch, velocity.Length / Math.Max(1f, _drift.Length) * SparkStretch));
                        Place(transform, space, _spark.PositionAt(seconds), velocity.AngleDegrees, _size * stretch, _size * SparkThicknessShare);
                        SetAlpha(_spark.AlphaAt(seconds));
                        break;
                    }
                    case VfxParticleKind.Puff:
                    {
                        float puff = _size * (1f + life * PuffGrowth);
                        Place(transform, space, _origin + _drift * seconds, 0f, puff, puff * HeightPerWidth);
                        SetAlpha(1f - life);
                        break;
                    }
                    case VfxParticleKind.Ring:
                    {
                        float eased = 1f - (1f - life) * (1f - life);
                        float ring = _size * (RingStartShare + (1f - RingStartShare) * eased);
                        Place(transform, space, _origin, 0f, ring, ring * HeightPerWidth);
                        SetAlpha(1f - life);
                        break;
                    }
                    case VfxParticleKind.Flash:
                    {
                        float flash = _size * (1f + life * FlashGrowth);
                        Place(transform, space, _origin, 0f, flash, flash * HeightPerWidth);
                        SetAlpha((1f - life) * (1f - life));
                        break;
                    }
                    default:
                    {
                        Vec2 position = _debris.PositionAt(seconds, out float rotation);
                        bool puddle = seconds >= _landingSeconds;
                        if (puddle) Place(transform, space, position, 0f, _size * PuddleWidthMultiplier, _size * PuddleHeightMultiplier);
                        else Place(transform, space, position, rotation, _size, _size * HeightPerWidth);
                        break;
                    }
                }
            }

            /// <summary>The sprite's height over its width: its own shape, kept when it is drawn at a size.</summary>
            private float HeightPerWidth
            {
                get
                {
                    Sprite sprite = Renderer.sprite;
                    return sprite != null && sprite.rect.width > 0f ? sprite.rect.height / sprite.rect.width : 1f;
                }
            }

            public void Hide()
            {
                if (Renderer != null && Renderer.gameObject.activeSelf) Renderer.gameObject.SetActive(false);
            }

            private void SetAlpha(float alpha)
            {
                Color color = _color;
                color.a *= XMath.Clamp01(alpha);
                Renderer.color = color;
            }

            /// <summary>Draws the piece this wide and this tall (arena units): its sprite is one world unit wide and its own shape tall.</summary>
            private void Place(Transform transform, ArenaSpace space, Vec2 position, float rotationDegrees, float widthUnits, float heightUnits)
            {
                transform.SetLocalPositionAndRotation(space.ToWorld(position), Quaternion.Euler(0f, 0f, rotationDegrees));
                transform.localScale = new Vector3(space.ToWorldLength(widthUnits), space.ToWorldLength(heightUnits) / HeightPerWidth, 1f);
            }
        }

        private readonly struct ActiveArc
        {
            public VfxArc Spec { get; }
            public SimTime Spawn { get; }
            public uint Seed { get; }
            public int Line { get; }

            public ActiveArc(VfxArc spec, SimTime spawn, uint seed, int line)
            {
                Spec = spec;
                Spawn = spawn;
                Seed = seed;
                Line = line;
            }
        }

        private readonly struct StruckPart
        {
            public Side Side { get; }
            public BodyPart Part { get; }
            public SimTime Spawn { get; }

            public StruckPart(Side side, BodyPart part, SimTime spawn)
            {
                Side = side;
                Part = part;
                Spawn = spawn;
            }
        }

        private readonly struct Flinch
        {
            public Side Side { get; }
            public SimTime Spawn { get; }
            public float Degrees { get; }
            public float PushUnits { get; }

            public Flinch(Side side, SimTime spawn, float degrees, float pushUnits)
            {
                Side = side;
                Spawn = spawn;
                Degrees = degrees;
                PushUnits = pushUnits;
            }
        }
    }
}
