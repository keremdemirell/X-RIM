using XRim.Core;
using XRim.Rules.Events;
using XRim.Simulation.Recording;

namespace XRim.Presentation.Playback
{
    /// <summary>
    /// What a fighter's HP was at a moment of a played turn, so the HP bars drop as playback passes each hit rather than when the
    /// turn resolved. It reads the turn as simulated (<see cref="TurnResult.Report"/>), before any sudden-death setup reset HP.
    /// </summary>
    public static class PlaybackHitPoints
    {
        /// <summary>The resolved HP with every hit still ahead of <paramref name="now"/> added back.</summary>
        public static float HpAt(TurnResult turn, SimTime now, Side side)
        {
            Guard.NotNull(turn, nameof(turn));
            float hp = turn.Report.ResolvedState.Fighters[side].Hp;
            foreach (MatchEvent matchEvent in turn.Timeline.Events)
            {
                if (matchEvent.Time > now && matchEvent is HitLandedEvent hit && hit.Hit.Victim == side) hp += hit.Damage.HpDamage;
            }

            return hp;
        }
    }
}
