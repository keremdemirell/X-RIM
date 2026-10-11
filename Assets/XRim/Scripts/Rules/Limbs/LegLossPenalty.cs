namespace XRim.Rules.Limbs
{
    /// <summary>How losing legs limits body moves (GDD §12 open point; the options listed there that change only planning).</summary>
    public enum LegLossPenalty
    {
        /// <summary>No limit: a dummy without legs still plans every move (the behaviour before Session 11).</summary>
        NoPenalty = 0,

        /// <summary>
        /// Designer 2026-10-10 (recommended default): with one leg left, no jump and no lunge (crouch and step back stay); with
        /// none, no body move at all.
        /// </summary>
        NoJumpOrLungeOnOneLeg = 1,
    }
}
