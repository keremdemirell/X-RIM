namespace XRim.App
{
    /// <summary>Who plans each side. All three run offline through LocalTurnAuthority.</summary>
    public enum MatchMode
    {
        HumanVsBot = 0,

        /// <summary>One person enters both players' inputs on one device (debug/offline prototype).</summary>
        HotSeat = 1,

        /// <summary>Both sides are bots; useful for soak tests and balance batches.</summary>
        BotVsBot = 2,
    }
}
