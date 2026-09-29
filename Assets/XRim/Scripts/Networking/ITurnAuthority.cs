using System;
using XRim.Core;
using XRim.Core.Gdd;
using XRim.Rules.Match;
using XRim.Rules.Planning;
using XRim.Simulation.Recording;

namespace XRim.Networking
{
    /// <summary>
    /// Whoever owns the match: runs the rules state machine, validates inputs on its own clock and produces
    /// each TurnResult. <see cref="LocalTurnAuthority"/> does it in-process (offline prototype, bots, hot-seat,
    /// tests). A future remote client would forward to a server hosting the same core, so the client code
    /// above this interface does not change.
    /// During planning only public state reaches the opponent; body moves, paths and signature moves arrive
    /// only inside the TurnResult.
    /// </summary>
    [GddTbd("§18", "Networking model", Proposal = "Server-authoritative")]
    public interface ITurnAuthority
    {
        event Action<MatchStartInfo> MatchStarted;
        event Action<PlanningWindow> PlanningStarted;

        /// <summary>Weapon switches and Ready presses, visible to the opponent immediately.</summary>
        event Action<Side, PublicPlanningState> PublicStateChanged;

        /// <summary>Planning is over for this turn (both Ready or the timer ended); plans are locked.</summary>
        event Action<int> PlanningLocked;

        event Action<TurnResult> TurnResolved;
        event Action<MatchOutcome> MatchEnded;

        void Start();

        /// <summary>Advance timers (local) or pump messages (remote). Call once per frame.</summary>
        void Tick();

        /// <summary>
        /// Sends one planning input for a side. A remote implementation returns the client-side check;
        /// the server may still reject the command.
        /// </summary>
        CommandResult Send(Side side, PlanningCommand command);
    }
}
