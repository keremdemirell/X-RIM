namespace XRim.App
{
    /// <summary>Where a hot-seat turn is (<see cref="HotSeatCoordinator"/>).</summary>
    public enum HotSeatStage
    {
        /// <summary>Not planning: the turn is executing or playing back, or the match is over. The clock runs.</summary>
        Idle = 0,

        /// <summary>The cover screen asks the next player to take the device. Nobody's input counts and the planning timer stands still.</summary>
        Cover = 1,

        /// <summary>One player plans alone; the other side's input is off.</summary>
        Planning = 2,
    }
}
