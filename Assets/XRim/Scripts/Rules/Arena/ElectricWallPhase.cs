namespace XRim.Rules.Arena
{
    public enum ElectricWallPhase
    {
        Inactive = 0,

        /// <summary>Turn N after a backward swipe: the wall appears behind the dummy, no damage.</summary>
        Warning = 1,

        /// <summary>Turn N+1 and later while retreating: electric damage, bounce, and the wall advances.</summary>
        Active = 2,
    }
}
