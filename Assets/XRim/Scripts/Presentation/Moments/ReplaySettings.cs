using XRim.Config;
using XRim.Core;

namespace XRim.Presentation.Moments
{
    /// <summary>How the instant replay plays (presentation only): the stretch around the moment, its speed and its camera. Plain data.</summary>
    public sealed class ReplaySettings
    {
        /// <summary>Seconds of the turn before the moment the replay starts.</summary>
        public float LeadSeconds;

        /// <summary>Seconds of the turn after the moment it ends.</summary>
        public float TailSeconds;

        /// <summary>The fastest it plays (1 = real time); the moment's own slow motion still goes slower.</summary>
        public float Speed = 1f;

        /// <summary>The camera closes in to this share of the view's size.</summary>
        public float ZoomViewFraction = 1f;

        /// <summary>Real seconds the camera takes to close in.</summary>
        public float ZoomSeconds;

        public static ReplaySettings From(FeelConfig config)
        {
            Guard.NotNull(config, nameof(config));
            return new ReplaySettings
            {
                LeadSeconds = config.ReplayLeadSeconds,
                TailSeconds = config.ReplayTailSeconds,
                Speed = config.ReplaySpeed,
                ZoomViewFraction = config.ReplayZoomViewFraction,
                ZoomSeconds = config.ReplayZoomSeconds,
            };
        }

        /// <summary>The stretch of the turn the replay shows: from the lead before the moment to the tail after it, within the turn.</summary>
        public void WindowAround(SimTime moment, SimTime turnDuration, out SimTime start, out SimTime end)
        {
            SimTime lead = SimTime.FromSeconds(System.Math.Max(0f, LeadSeconds));
            SimTime tail = SimTime.FromSeconds(System.Math.Max(0f, TailSeconds));
            start = moment - lead < SimTime.Zero ? SimTime.Zero : moment - lead;
            end = moment + tail > turnDuration ? turnDuration : moment + tail;
            if (end < start) end = start;
        }
    }
}
