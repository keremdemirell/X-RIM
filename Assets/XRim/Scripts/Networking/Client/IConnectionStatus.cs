using System;
using XRim.Networking.Protocol;
using XRim.Rules.Planning;
using XRim.Simulation.Recording;

namespace XRim.Networking.Client
{
    /// <summary>
    /// What an online authority can say beyond <see cref="ITurnAuthority"/> (Session 19, approved change 4): the state of the phone's own link,
    /// the opponent's connection (GDD §3; "Opponent reconnecting…", designer 2026-10-25, Q4), a command the server refused after the phone had
    /// accepted it, and the board after a rejoin. The HUD reads it when the authority offers it; an offline authority does not.
    /// </summary>
    public interface IConnectionStatus
    {
        /// <summary>This phone's link to the server is up.</summary>
        bool IsConnected { get; }

        /// <summary>This phone's link went up (rejoined) or down (dropped; it rejoins by itself).</summary>
        event Action<bool> ConnectionChanged;

        /// <summary>The link ended for good: the match is over, or the server refused or stopped, or the connection could not be kept.</summary>
        event Action<GoodbyeReason> Ended;

        /// <summary>The opponent dropped, came back, or the server stopped waiting for them (with how long it still waits).</summary>
        event Action<ConnectionNoticeMessage> OpponentConnectionChanged;

        /// <summary>
        /// The server refused a command this phone had accepted (it reached the server too late); the phone's copy of the plan is put back
        /// to what the server holds.
        /// </summary>
        event Action<CommandRejection> CommandRefused;

        /// <summary>After a rejoin: the board as it stands now (a turn may have resolved while the phone was away).</summary>
        event Action<BoardSnapshot> Resynchronized;

        /// <summary>
        /// The phone's copy of the player's plan was rebuilt from the plan the server holds (after a refusal, or on a rejoin mid-planning), so a
        /// HUD that keeps its own record of the plan can catch up.
        /// </summary>
        event Action OwnPlanRestored;
    }
}
