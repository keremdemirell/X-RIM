namespace XRim.Bots
{
    /// <summary>Which brain plays a bot side. The numbers are stored in the Bots tuning asset: never renumber them.</summary>
    public enum BotBrainKind
    {
        /// <summary>Reads the public board and tries to land hits (<see cref="HeuristicBotBrain"/>, Session 16).</summary>
        Heuristic = 0,

        /// <summary>Random valid plans (<see cref="RandomBotBrain"/>, the Session 03 debug bot).</summary>
        Random = 1,
    }
}
