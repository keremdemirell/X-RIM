namespace XRim.Economy
{
    /// <summary>
    /// What kind of match was played, as far as earned currency is concerned (GDD §16). The App layer maps its mode onto this; Economy
    /// never knows about the modes themselves.
    /// </summary>
    public enum MatchKind
    {
        /// <summary>One human against the heuristic bot: the only match that pays by default (designer 2026-10-25), because it is the only way to earn before online play.</summary>
        VersusBot = 0,

        /// <summary>Two people on one device share one account, so it pays nothing by default (they could farm themselves).</summary>
        HotSeat = 1,

        /// <summary>A bot of the player's choosing, or two bots to watch: no stakes, so nothing is paid by default.</summary>
        Practice = 2,

        /// <summary>
        /// Another player through the online server (Session 19): pays nothing by default until the server decides rewards (designer
        /// 2026-10-25, Q2), because a reward the phone decides can be faked.
        /// </summary>
        Online = 3,
    }
}
