using System;
using XRim.Core;

namespace XRim.Simulation
{
    /// <summary>
    /// The simulation clock (GDD §9, §18): an integer step count at a fixed rate. Time is always computed from
    /// the step index, never accumulated, so it cannot drift. Never read UnityEngine.Time in the simulation.
    /// </summary>
    public sealed class SimClock
    {
        public int StepRateHz { get; }
        public int Step { get; private set; }
        public float StepSeconds { get; }

        public SimClock(int stepRateHz)
        {
            StepRateHz = Guard.Positive(stepRateHz, nameof(stepRateHz));
            StepSeconds = 1f / stepRateHz;
        }

        public SimTime Now => TimeAtStep(Step);

        public void Advance() => Step++;

        public SimTime TimeAtStep(int step) => new SimTime(step * SimTime.MicrosecondsPerSecond / StepRateHz);

        /// <summary>The step whose motion a time falls in: step k covers the times after step k − 1's end, up to its own end.</summary>
        public int StepContaining(SimTime time) => (int)Math.Ceiling(time.Microseconds * (double)StepRateHz / SimTime.MicrosecondsPerSecond);

        /// <summary>Number of whole steps needed to cover a duration, e.g. the execution hard cap.</summary>
        public int StepsFor(double seconds) => (int)Math.Ceiling(seconds * StepRateHz);
    }
}
