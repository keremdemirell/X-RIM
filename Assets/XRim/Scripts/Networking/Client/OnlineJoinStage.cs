namespace XRim.Networking.Client
{
    /// <summary>Where an <see cref="OnlineMatchJoin"/> is, before the match starts.</summary>
    public enum OnlineJoinStage
    {
        /// <summary>Connecting and saying hello, then waiting for the server to find an opponent.</summary>
        FindingOpponent = 0,

        /// <summary>An opponent is found: the player picks a loadout (blind, GDD §6).</summary>
        Picking = 1,

        /// <summary>The pick is sent; the opponent is still picking.</summary>
        WaitingForPicks = 2,

        /// <summary>Both loadouts are revealed: the match can start (<see cref="OnlineMatchJoin.CreateAuthority"/>).</summary>
        Ready = 3,

        /// <summary>The link ended before the match started (see <see cref="ServerLink.EndReason"/>).</summary>
        Ended = 4,
    }
}
