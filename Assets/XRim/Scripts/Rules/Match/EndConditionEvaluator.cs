using System;
using XRim.Core.Gdd;
using XRim.Rules.Settings;

namespace XRim.Rules.Match
{
    /// <summary>
    /// The single place end conditions are checked after each turn: KO (one dummy at 0 HP), double KO
    /// (sudden death), forfeit (idle turns) and the turn cap (sudden death). Keeping them in one ordered
    /// list makes the order easy to change and test.
    /// </summary>
    [GddTbd("§3", "Order of end conditions when they happen in the same turn (not covered by the GDD)")]
    public sealed class EndConditionEvaluator
    {
        public EndCheckResult Evaluate(ExecutionReport report, RulesSettings settings)
        {
            // Placeholder: architecture setup only. Gameplay implementation comes later.
            throw new NotImplementedException("EndConditionEvaluator.Evaluate is not implemented yet.");
        }
    }
}
