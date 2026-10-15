using System.Collections.Generic;
using XRim.Core;
using XRim.Presentation.Playback;
using XRim.Simulation.Physics;
using XRim.Simulation.Recording;

namespace XRim.Presentation.Vfx
{
    /// <summary>
    /// The recent path of a weapon's tip, for the fading streak behind a swing (presentation only; trails can become a cosmetic, GDD §16
    /// "neon trails"): the recorded tip positions over the last stretch of the turn, newest last, back to where the tip was still moving
    /// fast. A resting or dropped weapon leaves none. Read from the recording, so it follows playback exactly, slow motion included.
    /// </summary>
    public static class WeaponTrail
    {
        /// <param name="shape">The held item's layout (its tip follows the path).</param>
        /// <param name="into">Filled with the trail, oldest point first; empty when there is none.</param>
        public static void Collect(TurnTimeline timeline, Side side, SimTime now, float windowSeconds, float minSpeedUnitsPerSecond,
            HeldItemShape shape, List<Vec2> into)
        {
            Guard.NotNull(timeline, nameof(timeline));
            Guard.NotNull(into, nameof(into));
            into.Clear();
            IReadOnlyList<TimelineFrame> frames = timeline.Frames;
            if (frames.Count < 2 || windowSeconds <= 0f) return;

            int latest = MatchEventLocator.FrameIndexAt(frames, now);
            if (latest < 0 || frames[latest].Time > now || !TryTip(frames[latest].Pose, side, shape, out Vec2 newer)) return;
            into.Add(newer);
            double oldest = now.Seconds - windowSeconds;
            for (int i = latest - 1; i >= 0; i--)
            {
                TimelineFrame frame = frames[i];
                if (frame.Time.Seconds < oldest || !TryTip(frame.Pose, side, shape, out Vec2 older)) break;
                double seconds = (frames[i + 1].Time - frame.Time).Seconds;
                if (seconds <= 0.0 || Vec2.Distance(newer, older) / seconds < minSpeedUnitsPerSecond) break;
                into.Add(older);
                newer = older;
            }

            if (into.Count < 2) into.Clear();
            else into.Reverse();
        }

        private static bool TryTip(PoseSnapshot pose, Side side, HeldItemShape shape, out Vec2 tip)
        {
            FighterPose fighter = pose.Get(side);
            tip = fighter.HasHeldItem ? shape.PathPointAt(fighter.HeldItem) : default;
            return fighter.HasHeldItem;
        }
    }
}
