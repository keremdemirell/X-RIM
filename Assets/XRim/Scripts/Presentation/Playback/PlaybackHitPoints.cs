using XRim.Core;
using XRim.Rules.Events;
using XRim.Simulation.Recording;

namespace XRim.Presentation.Playback
{
    /// <summary>
    /// What a fighter's HP was at a moment of a played turn, so the HP bars drop as playback passes each hit or electric-wall zap (GDD
    /// §13) rather than when the turn resolved. It reads the turn as simulated (<see cref="TurnResult.Report"/>), before any sudden-death
    /// setup reset HP.
    /// </summary>
    public static class PlaybackHitPoints
    {
        /// <summary>The resolved HP with every hit and zap still ahead of <paramref name="now"/> added back.</summary>
        public static float HpAt(TurnResult turn, SimTime now, Side side)
        {
            Guard.NotNull(turn, nameof(turn));
            float hp = turn.Report.ResolvedState.Fighters[side].Hp;
            foreach (MatchEvent matchEvent in turn.Timeline.Events)
            {
                if (matchEvent.Time > now) hp += HpLost(matchEvent, side);
            }

            return hp;
        }

        /// <summary>The HP an event took from a side: a hit on it, or its own wall's zap.</summary>
        public static float HpLost(MatchEvent matchEvent, Side side)
        {
            switch (matchEvent)
            {
                case HitLandedEvent hit when hit.Hit.Victim == side: return hit.Damage.HpDamage;
                case ElectricWallEvent zap when zap.Side == side: return zap.HpDamage;
                default: return 0f;
            }
        }
    }
}
