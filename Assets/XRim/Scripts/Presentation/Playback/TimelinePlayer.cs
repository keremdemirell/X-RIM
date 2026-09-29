using System;
using System.Collections.Generic;
using XRim.Core;
using XRim.Rules.Events;
using XRim.Simulation.Physics;
using XRim.Simulation.Recording;

namespace XRim.Presentation.Playback
{
    /// <summary>
    /// Plays a recorded turn at any speed: real time, slow motion, paused, frame-stepped or scrubbed. The turn was
    /// already resolved, so playback speed and feel effects can never change the outcome.
    /// Events fire once, in time order, when playback passes them; scrubbing backwards does not re-fire them.
    /// </summary>
    public sealed class TimelinePlayer
    {
        private const float RealTimeSpeed = 1f;

        private TurnTimeline _timeline;
        private int _nextEventIndex;

        public event Action<MatchEvent> EventReached;
        public event Action<PoseSnapshot> PoseChanged;

        public TurnTimeline Timeline => _timeline;
        public SimTime CurrentTime { get; private set; }

        /// <summary>1 = real time, 0.25 = quarter-speed slow motion.</summary>
        public float PlaybackSpeed { get; set; } = RealTimeSpeed;

        public bool IsPaused { get; set; }
        public bool IsFinished => _timeline == null || CurrentTime >= _timeline.Duration;

        public void Play(TurnTimeline timeline)
        {
            _timeline = Guard.NotNull(timeline, nameof(timeline));
            _nextEventIndex = 0;
            CurrentTime = SimTime.Zero;
            IsPaused = false;
            FireEventsUpTo(CurrentTime);
            EmitPose();
        }

        /// <summary>Advances by real (screen) time scaled by <see cref="PlaybackSpeed"/>.</summary>
        public void Advance(float realDeltaSeconds)
        {
            if (_timeline == null || IsPaused) return;
            Seek(CurrentTime + SimTime.FromSeconds(realDeltaSeconds * PlaybackSpeed));
        }

        public void Seek(SimTime time)
        {
            if (_timeline == null) return;
            SimTime target = time < SimTime.Zero ? SimTime.Zero : time > _timeline.Duration ? _timeline.Duration : time;
            if (target < CurrentTime) _nextEventIndex = FirstEventAfter(target);
            CurrentTime = target;
            FireEventsUpTo(target);
            EmitPose();
        }

        /// <summary>Moves by whole recorded frames (debug frame-step). Negative steps go back.</summary>
        public void StepFrames(int frameCount)
        {
            if (_timeline == null || _timeline.Frames.Count == 0) return;
            int current = Math.Max(FrameIndexAt(CurrentTime), 0);
            int target = Math.Max(0, Math.Min(_timeline.Frames.Count - 1, current + frameCount));
            Seek(_timeline.Frames[target].Time);
        }

        private void FireEventsUpTo(SimTime time)
        {
            IReadOnlyList<MatchEvent> events = _timeline.Events;
            while (_nextEventIndex < events.Count && events[_nextEventIndex].Time <= time)
            {
                MatchEvent matchEvent = events[_nextEventIndex];
                _nextEventIndex++;
                EventReached?.Invoke(matchEvent);
            }
        }

        private int FirstEventAfter(SimTime time)
        {
            IReadOnlyList<MatchEvent> events = _timeline.Events;
            int index = 0;
            while (index < events.Count && events[index].Time <= time) index++;
            return index;
        }

        /// <summary>Index of the last frame at or before a time, or -1.</summary>
        private int FrameIndexAt(SimTime time)
        {
            IReadOnlyList<TimelineFrame> frames = _timeline.Frames;
            int low = 0, high = frames.Count - 1, found = -1;
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

        private void EmitPose()
        {
            int index = FrameIndexAt(CurrentTime);
            if (index >= 0) PoseChanged?.Invoke(_timeline.Frames[index].Pose);
        }
    }
}
