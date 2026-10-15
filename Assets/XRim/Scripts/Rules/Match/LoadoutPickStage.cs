namespace XRim.Rules.Match
{
    /// <summary>Where the pre-match pick is (<see cref="LoadoutPickSession"/>).</summary>
    public enum LoadoutPickStage
    {
        /// <summary>The device changes hands (hot-seat): nothing of the last player's picks may show; a tap hands over.</summary>
        HandOver = 0,

        /// <summary>One human is choosing their 3 weapons.</summary>
        Picking = 1,

        /// <summary>Everyone is locked in: both loadouts are shown (GDD §6, Decided); a tap starts the match.</summary>
        Reveal = 2,

        /// <summary>The reveal was confirmed: the match can start.</summary>
        Done = 3,
    }
}
