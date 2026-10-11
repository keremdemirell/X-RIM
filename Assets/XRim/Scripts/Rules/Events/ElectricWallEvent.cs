using XRim.Core;
using XRim.Rules.Arena;

namespace XRim.Rules.Events
{
    /// <summary>
    /// Something a player's electric wall did (GDD §13): the warning springing up behind the dummy (<see cref="ElectricWallPhase.Warning"/>,
    /// no damage), or the dummy touching its active wall (<see cref="ElectricWallPhase.Active"/>: the zap, its damage, and where the wall
    /// stands after it advanced).
    /// </summary>
    public sealed class ElectricWallEvent : MatchEvent
    {
        /// <summary>Whose wall it is: the dummy it stands behind, and the only one it can zap.</summary>
        public Side Side { get; }

        public ElectricWallPhase Phase { get; }

        /// <summary>0 for the warning wall.</summary>
        public float HpDamage { get; }

        /// <summary>Where the wall's face stands after this event, along the arena X axis (arena units).</summary>
        public float PositionUnits { get; }

        /// <summary>
        /// A later touch in the same turn, after the retreat's own zap: the dummy was knocked into the wall. It only deals damage, and only
        /// with <c>ElectricWallSettings.DamageWhenKnockedInto</c> on (§13, TBD).
        /// </summary>
        public bool IsKnockedInto { get; }

        public ElectricWallEvent(SimTime time, Side side, ElectricWallPhase phase, float hpDamage, float positionUnits, bool isKnockedInto = false)
            : base(time)
        {
            Side = side;
            Phase = phase;
            HpDamage = hpDamage;
            PositionUnits = positionUnits;
            IsKnockedInto = isKnockedInto;
        }
    }
}
