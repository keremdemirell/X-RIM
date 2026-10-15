using XRim.Core;
using XRim.Presentation.Playback;
using XRim.Rules.Events;
using XRim.Simulation.Physics;
using XRim.Simulation.Recording;

namespace XRim.Presentation.Moments
{
    /// <summary>
    /// What to replay when a replay is asked for (the debug tools' "replay the last turn"): the turn's legendary moment when it has one,
    /// otherwise its hardest hit, otherwise its middle with the camera between the two dummies. Pure: tested without Unity.
    /// </summary>
    public static class RequestedMoment
    {
        public static LegendaryMoment Pick(TurnResult turn, IMomentDetector detector)
        {
            Guard.NotNull(turn, nameof(turn));
            if (detector != null && detector.TryDetect(turn, out LegendaryMoment moment)) return moment;

            HitLandedEvent hardest = null;
            foreach (MatchEvent matchEvent in turn.Timeline.Events)
            {
                if (matchEvent is HitLandedEvent hit && (hardest == null || hit.Damage.Damage > hardest.Damage.Damage)) hardest = hit;
            }

            if (hardest != null)
            {
                MatchEventLocator.TryLocate(turn, hardest, out Vec2 point);
                Side attacker = hardest.Hit.Attacker;
                return new LegendaryMoment(MomentKind.Requested, hardest.Time, attacker, point, $"{attacker}'s hardest hit (asked for)");
            }

            SimTime middle = SimTime.FromSeconds(turn.Timeline.Duration.Seconds / 2.0);
            PoseSnapshot pose = MatchEventLocator.PoseAt(turn.Timeline, middle);
            Vec2 between = pose != null ? (MatchEventLocator.TorsoCentre(pose.Left) + MatchEventLocator.TorsoCentre(pose.Right)) * 0.5f : Vec2.Zero;
            return new LegendaryMoment(MomentKind.Requested, middle, null, between, "No moment or hit: the turn's middle (asked for)");
        }
    }
}
