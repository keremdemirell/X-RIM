using XRim.Core;
using XRim.Rules.Arena;
using XRim.Rules.Events;
using XRim.Rules.Match;
using XRim.Rules.Settings;
using XRim.Simulation.Physics;

namespace XRim.Simulation.Execution
{
    /// <summary>
    /// Both electric walls during one turn (GDD §13; the designer's geometry P1, 2026-10-11): the physical half of
    /// <see cref="ElectricWallRules"/>, which decides everything. The rules state (<see cref="MatchState.Walls"/>) says where each wall ends
    /// up; this class moves the wall bodies there and says where they stand in between.
    /// <list type="bullet">
    /// <item>A warning raised this turn springs up when its dummy's step back ends (<see cref="Raise"/>); from then on it holds the dummy
    /// in front of it (<see cref="BackLimitAt"/>).</item>
    /// <item>An active wall stands from the turn's start. The owner's first touch is the zap (<see cref="Touch"/>); from then on the wall
    /// advances toward the opponent over the bounce time and holds the dummy in front of it, so the advance shoves the dummy forward. A
    /// step back that never reaches the wall is still owed its zap (<see cref="IsOwedAZap"/>): only the input counts.</item>
    /// <item>A later touch in the same turn deals damage again only after a hit knocked the dummy back since the last zap, and only with
    /// <see cref="ElectricWallSettings.DamageWhenKnockedInto"/> on (§13, TBD): pressing against the wall, or a second body part touching
    /// it, is never a new zap.</item>
    /// </list>
    /// </summary>
    public sealed class TurnWalls
    {
        private static readonly Side[] BothSides = { Side.Left, Side.Right };

        private readonly IPhysicsWorld _world;
        private readonly ElectricWallRules _rules = new ElectricWallRules();
        private readonly Wall[] _walls = { new Wall(), new Wall() };
        private MatchState _state;
        private ArenaEdges _edges;
        private ElectricWallSettings _settings;
        private float _advanceSeconds;

        public TurnWalls(IPhysicsWorld world)
        {
            _world = Guard.NotNull(world, nameof(world));
        }

        /// <param name="state">The turn's rules state, walls as the lock left them; the rules change it as walls rise and zap.</param>
        /// <param name="advanceSeconds">How long a zap's advance takes (the bounce's time, so the wall follows the dummy it throws).</param>
        public void Begin(MatchState state, ArenaEdges edges, ElectricWallSettings settings, float advanceSeconds)
        {
            _state = Guard.NotNull(state, nameof(state));
            _settings = Guard.NotNull(settings, nameof(settings));
            _edges = edges;
            _advanceSeconds = advanceSeconds;
            foreach (Side side in BothSides)
            {
                ElectricWallState wall = state.Walls[side];
                _walls[(int)side].Reset(wall.IsUp ? wall.PositionUnits : 0f);
            }
        }

        /// <summary>The side's warning springs up this turn, when its step back ends.</summary>
        public bool IsRaisePending(Side side)
        {
            ElectricWallState wall = _state.Walls[side];
            return wall.Phase == ElectricWallPhase.Warning && !wall.IsUp;
        }

        /// <summary>The side retreated into its active wall this turn and has not been zapped yet.</summary>
        public bool IsOwedAZap(Side side)
        {
            ElectricWallState wall = _state.Walls[side];
            return wall.Phase == ElectricWallPhase.Active && wall.IsUp && !_walls[(int)side].RetreatZapDone;
        }

        /// <summary>The warning springs up behind the dummy's pelvis, in the world too. Returns its event for the timeline.</summary>
        public ElectricWallEvent Raise(Side side, SimTime time, float pelvisXUnits)
        {
            ElectricWallEvent raised = _rules.RaiseWarning(time, side, _state.Walls[side], pelvisXUnits, _edges, _settings);
            Wall wall = _walls[(int)side];
            wall.Reset(raised.PositionUnits);
            wall.HoldsBack = true;
            _world.SetElectricWall(side, raised.PositionUnits);
            return raised;
        }

        /// <summary>
        /// The side's body touched its own wall (or its step back ended without reaching it, see <see cref="IsOwedAZap"/>). Returns the zap
        /// for the simulation to apply (bounce, death), or null when the touch does nothing. The HP loss is already in the rules state.
        /// </summary>
        /// <param name="lastHitKnock">When a hit last knocked the dummy back this turn; null if none did.</param>
        public ElectricWallZap Touch(Side side, SimTime time, SimTime? lastHitKnock)
        {
            Wall wall = _walls[(int)side];
            if (!wall.RetreatZapDone)
            {
                ElectricWallZap zap = _rules.OnTouched(time, side, _state, false, _edges, _settings);
                if (zap == null) return null;
                wall.RetreatZapDone = true;
                wall.LastZapTime = time;
                wall.AdvanceTo(time, zap.WallPositionUnits, _advanceSeconds);
                wall.HoldsBack = true;
                return zap;
            }

            if (!lastHitKnock.HasValue || lastHitKnock.Value <= wall.LastZapTime) return null;
            ElectricWallZap knockedInto = _rules.OnTouched(time, side, _state, true, _edges, _settings);
            if (knockedInto != null) wall.LastZapTime = time;
            return knockedInto;
        }

        /// <summary>The x the side's body must stay in front of at this time (its standing wall), or null while nothing holds it back.</summary>
        public float? BackLimitAt(Side side, SimTime time)
        {
            Wall wall = _walls[(int)side];
            return wall.HoldsBack && _state.Walls[side].IsUp ? wall.XAt(time) : (float?)null;
        }

        /// <summary>Sends every advancing wall to where it is at this time (the end of the coming step).</summary>
        public void Step(SimTime time)
        {
            foreach (Side side in BothSides)
            {
                Wall wall = _walls[(int)side];
                if (!_state.Walls[side].IsUp) continue;
                float x = wall.XAt(time);
                if (x == wall.SentXUnits) continue;
                wall.SentXUnits = x;
                _world.SetElectricWall(side, x);
            }
        }

        /// <summary>Marks each wall in a captured pose: standing where it is at this time, or none.</summary>
        public void Mark(PoseSnapshot pose, SimTime time)
        {
            Guard.NotNull(pose, nameof(pose));
            foreach (Side side in BothSides)
            {
                ElectricWallState wall = _state.Walls[side];
                pose.SetWall(side, wall.IsUp ? new ElectricWallPose(wall.Phase, _walls[(int)side].XAt(time)) : default);
            }
        }

        /// <summary>A contact between a player's body and that player's own wall: the only touch that can zap (P1).</summary>
        public static bool TryReadTouch(ContactFacts facts, out Side owner)
        {
            owner = default;
            BodyTag wall, body;
            if (facts.A.Role == BodyRole.ElectricWall)
            {
                wall = facts.A;
                body = facts.B;
            }
            else if (facts.B.Role == BodyRole.ElectricWall)
            {
                wall = facts.B;
                body = facts.A;
            }
            else
            {
                return false;
            }

            if (!wall.Owner.HasValue || body.Role != BodyRole.BodyPart || body.Owner != wall.Owner) return false;
            owner = wall.Owner.Value;
            return true;
        }

        /// <summary>One wall this turn: where it stands, its advance, whether it holds its dummy back and whether it zapped.</summary>
        private sealed class Wall
        {
            private float _fromUnits;
            private float _toUnits;
            private SimTime _advanceStart;
            private float _advanceSeconds;

            public bool HoldsBack { get; set; }
            public bool RetreatZapDone { get; set; }
            public SimTime LastZapTime { get; set; }

            /// <summary>The last place sent to the world.</summary>
            public float SentXUnits { get; set; }

            public void Reset(float xUnits)
            {
                _fromUnits = xUnits;
                _toUnits = xUnits;
                _advanceStart = SimTime.Zero;
                _advanceSeconds = 0f;
                SentXUnits = xUnits;
                HoldsBack = false;
                RetreatZapDone = false;
                LastZapTime = SimTime.Zero;
            }

            /// <summary>From where it stands at this time to <paramref name="toUnits"/>, fast first and easing to a stop (like a knockback).</summary>
            public void AdvanceTo(SimTime start, float toUnits, float seconds)
            {
                _fromUnits = XAt(start);
                _toUnits = toUnits;
                _advanceStart = start;
                _advanceSeconds = seconds;
            }

            public float XAt(SimTime time)
            {
                if (time <= _advanceStart) return _fromUnits;
                float progress = _advanceSeconds > 0f ? XMath.Clamp01((float)((time - _advanceStart).Seconds / _advanceSeconds)) : 1f;
                float easedOut = 1f - (1f - progress) * (1f - progress);
                return _fromUnits + (_toUnits - _fromUnits) * easedOut;
            }
        }
    }
}
