using System.Globalization;
using XRim.Core;

namespace XRim.Bots.Balance
{
    /// <summary>One run of the balance batch: a matchup played with the limb durabilities scaled by a factor (1: the tuning as it is).</summary>
    public sealed class BalanceRun
    {
        public BalanceRun(BalanceMatchup matchup, float durabilityFactor)
        {
            Matchup = Guard.NotNull(matchup, nameof(matchup));
            DurabilityFactor = durabilityFactor;
        }

        public BalanceMatchup Matchup { get; }

        /// <summary>Arm and leg durability are multiplied by this for the run (a sweep toward the GDD §11 target); 1 leaves them as tuned.</summary>
        public float DurabilityFactor { get; }

        public bool IsBaseline => DurabilityFactor == 1f;

        public string Label => IsBaseline
            ? Matchup.Label
            : string.Format(CultureInfo.InvariantCulture, "{0}, limb durability ×{1:0.##}", Matchup.Label, DurabilityFactor);
    }
}
