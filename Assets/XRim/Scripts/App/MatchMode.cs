namespace XRim.App
{
    /// <summary>Who plans each side. All of them run offline through LocalTurnAuthority.</summary>
    public enum MatchMode
    {
        HumanVsBot = 0,

        /// <summary>One person enters both players' inputs on one device (debug/offline prototype).</summary>
        HotSeat = 1,

        /// <summary>Both sides are bots; useful for soak tests and balance batches.</summary>
        BotVsBot = 2,

        /// <summary>
        /// Debug sandbox (Editor and development builds): the debug tools plan both sides with the mouse and hand their plan
        /// sources to <see cref="MatchBootstrap.StartMatch"/>. The planning timer is frozen, so planning waits for Execute.
        /// </summary>
        Sandbox = 3,
    }
}
