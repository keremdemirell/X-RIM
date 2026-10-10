namespace XRim.App
{
    /// <summary>Who plans each side. All of them run offline through LocalTurnAuthority; the opponents are dev-only (D25).</summary>
    public enum MatchMode
    {
        /// <summary>A human on this device against the random debug bot.</summary>
        HumanVsBot = 0,

        /// <summary>
        /// Two people share one device and plan one after the other, with a cover screen in between (<see cref="HotSeatCoordinator"/>).
        /// </summary>
        HotSeat = 1,

        /// <summary>Both sides are bots: watch a match, soak tests and balance batches.</summary>
        BotVsBot = 2,

        /// <summary>
        /// Debug sandbox (Editor and development builds): the debug tools plan both sides with the mouse and hand their plan
        /// sources to <see cref="MatchBootstrap.StartMatch"/>. The planning timer is frozen, so planning waits for Execute.
        /// </summary>
        Sandbox = 3,
    }
}
