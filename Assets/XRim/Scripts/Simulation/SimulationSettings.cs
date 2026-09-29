using System;

namespace XRim.Simulation
{
    /// <summary>
    /// Technical tunables for the execution simulation. These are not GDD values; they exist so the GDD's
    /// timing rules (millisecond time-to-impact, 1.5 s hard cap) can be met and tuned for feel and device cost.
    /// </summary>
    [Serializable]
    public sealed class SimulationSettings
    {
        /// <summary>Fixed physics steps per second. High enough that a thin, fast rapier does not tunnel.</summary>
        public int StepRateHz = 240;

        /// <summary>Physics counts as settled when every body is slower than this...</summary>
        public float SettleLinearSpeedUnitsPerSecond = 5f;

        public float SettleAngularSpeedDegreesPerSecond = 10f;

        /// <summary>...for this many consecutive steps (GDD §3: execution ends when paths finish and physics settles).</summary>
        public int SettleStepsRequired = 6;

        /// <summary>1 = record a pose every step. Higher values shrink recordings (e.g. for network transfer).</summary>
        public int RecordEveryNthStep = 1;
    }
}
