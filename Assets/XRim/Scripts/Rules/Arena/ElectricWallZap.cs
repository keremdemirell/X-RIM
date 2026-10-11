using System.Collections.Generic;
using XRim.Core;
using XRim.Rules.Events;

namespace XRim.Rules.Arena
{
    /// <summary>
    /// What one zap did (GDD §13), for the simulation to apply: the HP already taken off in the rules state, whether the dummy died of
    /// it, how far it is thrown forward and where the wall stands afterwards. <see cref="Events"/> holds the <see cref="ElectricWallEvent"/>
    /// and, when the zap killed, the <see cref="FighterDiedEvent"/>.
    /// </summary>
    public sealed class ElectricWallZap
    {
        public Side Side { get; }

        public float HpDamage { get; }

        /// <summary>The zap took the dummy's HP to 0: it dies like a knocked-out dummy (GDD §11: limp, the torso splits).</summary>
        public bool Died { get; }

        /// <summary>A later touch in the same turn (the dummy was knocked into the wall): damage only, no bounce and no advance.</summary>
        public bool IsKnockedInto { get; }

        /// <summary>How far the dummy is thrown forward, toward the opponent; it stays there, like a knockback. 0 when knocked into.</summary>
        public float BounceDistanceUnits { get; }

        /// <summary>Where the wall's face stands after the zap: it advanced toward the opponent (unchanged when knocked into).</summary>
        public float WallPositionUnits { get; }

        public IReadOnlyList<MatchEvent> Events { get; }

        public ElectricWallZap(Side side, float hpDamage, bool died, bool isKnockedInto, float bounceDistanceUnits, float wallPositionUnits,
            IReadOnlyList<MatchEvent> events)
        {
            Side = side;
            HpDamage = hpDamage;
            Died = died;
            IsKnockedInto = isKnockedInto;
            BounceDistanceUnits = bounceDistanceUnits;
            WallPositionUnits = wallPositionUnits;
            Events = Guard.NotNull(events, nameof(events));
        }
    }
}
