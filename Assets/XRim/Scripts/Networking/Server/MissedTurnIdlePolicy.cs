using System;
using XRim.Core;
using XRim.Core.Gdd;
using XRim.Rules.Planning;

namespace XRim.Networking.Server
{
    /// <summary>
    /// The off option of <see cref="NetworkSettings.MissedTurnsCountAsIdle"/> (GDD §3, TBD): a turn whose plans locked while the player was
    /// disconnected never counts as idle, so it can never forfeit them (the forfeit counter starts again); every other turn is judged by
    /// the match's own policy.
    /// </summary>
    [GddTbd("§3", "Do missed turns count as idle turns?", Proposal = "Yes (designer 2026-10-25); this is the switchable alternative")]
    internal sealed class MissedTurnIdlePolicy : IIdleTurnPolicy
    {
        private readonly IIdleTurnPolicy _inner;
        private readonly Func<Side, bool> _missed;

        public MissedTurnIdlePolicy(IIdleTurnPolicy inner, Func<Side, bool> missed)
        {
            _inner = Guard.NotNull(inner, nameof(inner));
            _missed = Guard.NotNull(missed, nameof(missed));
        }

        public bool IsIdle(Side side, TurnPlan plan) => !_missed(side) && _inner.IsIdle(side, plan);
    }
}
