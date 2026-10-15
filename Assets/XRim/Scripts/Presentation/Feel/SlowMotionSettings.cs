using XRim.Config;
using XRim.Core;

namespace XRim.Presentation.Feel
{
    /// <summary>
    /// Which moments slow playback down, and how (GDD pillar 1; presentation only): a sever, a death (a KO), an interrupt counter-hit
    /// (GDD §9) and the decisive moment of a sudden-death turn (§14). Plain data read from <see cref="FeelConfig"/> at the start of each
    /// play.
    /// </summary>
    public sealed class SlowMotionSettings
    {
        public SlowMotionMoment Sever;
        public SlowMotionMoment KnockOut;
        public SlowMotionMoment CounterHit;
        public SlowMotionMoment SuddenDeathFinish;

        /// <summary>How long playback takes to slow into and speed out of a slowed stretch, in seconds of the turn.</summary>
        public float RampTurnSeconds;

        public static SlowMotionSettings From(FeelConfig config)
        {
            Guard.NotNull(config, nameof(config));
            return new SlowMotionSettings
            {
                Sever = new SlowMotionMoment(config.SeverSlowMotionSpeed, config.SeverSlowMotionRealSeconds, config.SeverSlowMotionLeadFraction),
                KnockOut = new SlowMotionMoment(config.KnockOutSlowMotionSpeed, config.KnockOutSlowMotionRealSeconds,
                    config.KnockOutSlowMotionLeadFraction),
                CounterHit = new SlowMotionMoment(config.CounterHitSlowMotionSpeed, config.CounterHitSlowMotionRealSeconds,
                    config.CounterHitSlowMotionLeadFraction),
                SuddenDeathFinish = new SlowMotionMoment(config.SuddenDeathFinishSlowMotionSpeed, config.SuddenDeathFinishSlowMotionRealSeconds,
                    config.SuddenDeathFinishSlowMotionLeadFraction),
                RampTurnSeconds = config.SlowMotionRampTurnSeconds,
            };
        }
    }
}
