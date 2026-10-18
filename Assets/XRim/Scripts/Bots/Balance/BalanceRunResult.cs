using System.Collections.Generic;
using XRim.Core;

namespace XRim.Bots.Balance
{
    /// <summary>One run's matches and the limb durabilities they were played with.</summary>
    public sealed class BalanceRunResult
    {
        private readonly List<BalanceMatchRecord> _records = new List<BalanceMatchRecord>();

        public BalanceRunResult(BalanceRun run, float armDurability, float legDurability)
        {
            Run = Guard.NotNull(run, nameof(run));
            ArmDurability = armDurability;
            LegDurability = legDurability;
        }

        public BalanceRun Run { get; }
        public float ArmDurability { get; }
        public float LegDurability { get; }
        public IReadOnlyList<BalanceMatchRecord> Records => _records;

        public void Add(BalanceMatchRecord record) => _records.Add(Guard.NotNull(record, nameof(record)));
    }
}
