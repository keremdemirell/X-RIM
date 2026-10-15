using XRim.Config;
using XRim.Core;

namespace XRim.Presentation.Moments
{
    /// <summary>Which kinds of legendary moments are looked for (each can be switched off in the Feel asset). Plain data.</summary>
    public sealed class MomentSettings
    {
        public bool KnockOuts = true;
        public bool SuddenDeathFinishes = true;
        public bool Severs = true;
        public bool DodgeCounters = true;
        public bool CounterHits = true;
        public bool Trades = true;
        public bool Crushes = true;

        public static MomentSettings From(FeelConfig config)
        {
            Guard.NotNull(config, nameof(config));
            return new MomentSettings
            {
                KnockOuts = config.ReplayKnockOuts,
                SuddenDeathFinishes = config.ReplaySuddenDeathFinishes,
                Severs = config.ReplaySevers,
                DodgeCounters = config.ReplayDodgeCounters,
                CounterHits = config.ReplayCounterHits,
                Trades = config.ReplayTrades,
                Crushes = config.ReplayCrushes,
            };
        }

        public bool Allows(MomentKind kind)
        {
            switch (kind)
            {
                case MomentKind.SuddenDeathFinish: return SuddenDeathFinishes;
                case MomentKind.DoubleKnockOut:
                case MomentKind.KnockOut:
                    return KnockOuts;
                case MomentKind.Sever: return Severs;
                case MomentKind.DodgeCounter: return DodgeCounters;
                case MomentKind.CounterHit: return CounterHits;
                case MomentKind.Trade: return Trades;
                case MomentKind.Requested: return true; // asked for, never detected
                default: return Crushes;
            }
        }
    }
}
