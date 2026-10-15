using System;
using System.Collections.Generic;
using System.Globalization;
using XRim.Core;
using XRim.Rules.Events;
using XRim.Simulation.Recording;

namespace XRim.Presentation.Feel
{
    /// <summary>
    /// How long a turn plays on screen and why (Session 13), planned from the feel values before it plays: its recorded length, the time
    /// slow motion adds, and the time the hitstops hold the picture (the longest freeze of each instant that freezes: freezes at one instant
    /// share one, as <see cref="HitStop"/> does). The camera settling at the end is measured, not planned.
    /// </summary>
    public readonly struct PacingBreakdown
    {
        public PacingBreakdown(float turnSeconds, float slowMotionSeconds, float hitStopSeconds, int freezes, int slowMotionStretches)
        {
            TurnSeconds = turnSeconds;
            SlowMotionSeconds = slowMotionSeconds;
            HitStopSeconds = hitStopSeconds;
            Freezes = freezes;
            SlowMotionStretches = slowMotionStretches;
        }

        /// <summary>The turn's recorded length: its timeline at 1×.</summary>
        public float TurnSeconds { get; }

        /// <summary>What slow motion adds on top of the recorded length.</summary>
        public float SlowMotionSeconds { get; }

        /// <summary>What the hitstops add: the picture held still.</summary>
        public float HitStopSeconds { get; }

        /// <summary>How many instants freeze.</summary>
        public int Freezes { get; }

        /// <summary>How many slow-motion stretches the turn has.</summary>
        public int SlowMotionStretches { get; }

        /// <summary>The planned on-screen time: the turn through its slow motion, plus its freezes.</summary>
        public float PlannedSeconds => TurnSeconds + SlowMotionSeconds + HitStopSeconds;

        public static PacingBreakdown Of(TurnResult turn, HitStopSettings hitStops, SlowMotionPlan slowMotion)
        {
            Guard.NotNull(turn, nameof(turn));
            Guard.NotNull(hitStops, nameof(hitStops));
            Guard.NotNull(slowMotion, nameof(slowMotion));
            float turnSeconds = (float)turn.Timeline.Duration.Seconds;
            float slowed = slowMotion.RealSecondsBetween(SimTime.Zero, turn.Timeline.Duration);
            float frozen = 0f;
            int freezes = 0;
            IReadOnlyList<MatchEvent> events = turn.Timeline.Events;
            int i = 0;
            while (i < events.Count)
            {
                SimTime instant = events[i].Time;
                float freeze = 0f;
                for (; i < events.Count && events[i].Time == instant; i++)
                {
                    freeze = Math.Max(freeze, hitStops.SecondsFor(events[i]));
                }

                frozen += freeze;
                if (freeze > 0f) freezes++;
            }

            return new PacingBreakdown(turnSeconds, slowed - turnSeconds, frozen, freezes, slowMotion.Windows.Count);
        }

        public override string ToString() => string.Format(CultureInfo.InvariantCulture,
            "{0:0.00} s = turn {1:0.000} s + slow motion {2:0.00} s ({3} stretch(es)) + hitstops {4:0.00} s ({5} freeze(s))",
            PlannedSeconds, TurnSeconds, SlowMotionSeconds, SlowMotionStretches, HitStopSeconds, Freezes);
    }
}
