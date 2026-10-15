using System.Collections.Generic;
using XRim.Core;
using XRim.Rules;
using XRim.Rules.Events;
using XRim.Simulation.Execution;
using XRim.Simulation.Physics;
using XRim.Simulation.Recording;

namespace XRim.Presentation.Playback
{
    /// <summary>
    /// Where in the arena a played event happened, for the effects that point at it (camera punch-in, sparks, debris): a clash or a
    /// block carries its contact point; a hit is found in the turn's raw contacts at the same instant (the touched part and the
    /// attacker's item), or falls back to the struck part; a sever is at the limb's joint; a death and a wall zap at the torso's middle.
    /// Pure arithmetic over the recorded <see cref="TurnResult"/>: it never changes it. Arena units, canonical (unflipped) arena.
    /// </summary>
    public static class MatchEventLocator
    {
        /// <summary>The place an event happened; false for events with no place (or a turn without frames).</summary>
        public static bool TryLocate(TurnResult turn, MatchEvent matchEvent, out Vec2 pointUnits)
        {
            Guard.NotNull(turn, nameof(turn));
            Guard.NotNull(matchEvent, nameof(matchEvent));
            pointUnits = default;
            switch (matchEvent)
            {
                case WeaponClashEvent clash:
                    pointUnits = clash.ContactPointUnits;
                    return true;
                case ShieldBlockEvent block:
                    pointUnits = block.ContactPointUnits;
                    return true;
                case HitLandedEvent hit:
                    return TryLocateHit(turn, hit, out pointUnits);
                case AttackInterruptedEvent interrupted:
                    HitLandedEvent cause = InterruptingHit(turn, interrupted);
                    if (cause != null) return TryLocateHit(turn, cause, out pointUnits);
                    return TryTorsoCentre(turn, interrupted.Time, interrupted.Interrupted, out pointUnits);
                case LimbSeveredEvent severed:
                    PoseSnapshot pose = PoseAt(turn.Timeline, severed.Time);
                    if (pose == null) return false;
                    pointUnits = pose.Get(severed.Side).Get(severed.Part).PositionUnits;
                    return true;
                case FighterDiedEvent died:
                    return TryTorsoCentre(turn, died.Time, died.Side, out pointUnits);
                case ElectricWallEvent wall:
                    return TryTorsoCentre(turn, wall.Time, wall.Side, out pointUnits);
                case BodyMoveStartedEvent move:
                    PoseSnapshot start = PoseAt(turn.Timeline, move.Time);
                    if (start == null) return false;
                    pointUnits = start.Get(move.Side).Get(BodyPart.Torso).PositionUnits;
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>The hit that landed at the same instant on the side whose attack was cancelled (GDD §9); null if none was recorded.</summary>
        public static HitLandedEvent InterruptingHit(TurnResult turn, AttackInterruptedEvent interrupted)
        {
            Guard.NotNull(turn, nameof(turn));
            Guard.NotNull(interrupted, nameof(interrupted));
            foreach (MatchEvent matchEvent in turn.Timeline.Events)
            {
                if (matchEvent is HitLandedEvent hit && hit.Time == interrupted.Time && hit.Hit.Victim == interrupted.Interrupted) return hit;
            }

            return null;
        }

        /// <summary>The recorded pose shown at a time: the last frame at or before it (the first frame before the turn starts); null without frames.</summary>
        public static PoseSnapshot PoseAt(TurnTimeline timeline, SimTime time)
        {
            Guard.NotNull(timeline, nameof(timeline));
            int index = FrameIndexAt(timeline.Frames, time);
            return index >= 0 ? timeline.Frames[index].Pose : null;
        }

        /// <summary>The index of the frame shown at a time: the last at or before it (0 before the turn starts); -1 without frames.</summary>
        public static int FrameIndexAt(IReadOnlyList<TimelineFrame> frames, SimTime time)
        {
            Guard.NotNull(frames, nameof(frames));
            if (frames.Count == 0) return -1;
            int low = 0, high = frames.Count - 1, found = 0;
            while (low <= high)
            {
                int mid = (low + high) / 2;
                if (frames[mid].Time <= time)
                {
                    found = mid;
                    low = mid + 1;
                }
                else
                {
                    high = mid - 1;
                }
            }

            return found;
        }

        /// <summary>The middle of a dummy's torso: halfway between its pelvis (the torso's pivot) and its neck (the head's pivot).</summary>
        public static Vec2 TorsoCentre(FighterPose pose)
        {
            Guard.NotNull(pose, nameof(pose));
            return (pose.Get(BodyPart.Torso).PositionUnits + pose.Get(BodyPart.Head).PositionUnits) * 0.5f;
        }

        private static bool TryTorsoCentre(TurnResult turn, SimTime time, Side side, out Vec2 pointUnits)
        {
            PoseSnapshot pose = PoseAt(turn.Timeline, time);
            pointUnits = pose != null ? TorsoCentre(pose.Get(side)) : default;
            return pose != null;
        }

        private static bool TryLocateHit(TurnResult turn, HitLandedEvent hit, out Vec2 pointUnits)
        {
            Side attacker = hit.Hit.Attacker;
            Side victim = hit.Hit.Victim;
            foreach (MatchEvent matchEvent in turn.Timeline.Events)
            {
                if (!(matchEvent is ContactEvent contact) || contact.Time != hit.Time) continue;
                ContactFacts facts = contact.Contact.Facts;
                if (IsStruck(facts.A, victim, hit.Hit.Part) && facts.B.Owner == attacker ||
                    IsStruck(facts.B, victim, hit.Hit.Part) && facts.A.Owner == attacker)
                {
                    pointUnits = facts.PointUnits;
                    return true;
                }
            }

            PoseSnapshot pose = PoseAt(turn.Timeline, hit.Time);
            pointUnits = default;
            if (pose == null) return false;
            pointUnits = StruckPartMiddle(pose.Get(victim), hit.Hit.Part);
            return true;
        }

        private static bool IsStruck(BodyTag tag, Side victim, BodyPart part) =>
            tag.Owner == victim && tag.Role == BodyRole.BodyPart && tag.Part == part;

        /// <summary>Roughly where a struck part is when no contact says: a limb's elbow or knee (its middle), the torso's middle, the head.</summary>
        private static Vec2 StruckPartMiddle(FighterPose pose, BodyPart part)
        {
            if (part == BodyPart.Torso) return TorsoCentre(pose);
            if (part.IsSeverable() && pose.HasLowerSegments) return pose.GetLower(part).PositionUnits;
            return pose.Get(part).PositionUnits;
        }
    }
}
