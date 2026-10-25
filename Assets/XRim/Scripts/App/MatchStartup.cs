namespace XRim.App
{
    /// <summary>What <see cref="MatchBootstrap"/> does when the scene starts.</summary>
    public enum MatchStartup
    {
        /// <summary>
        /// Show the game's menus (Session 18): the first-launch handedness pick on a new profile, then the main menu. The Match scene's
        /// startup, and the default of a scene built before this existed.
        /// </summary>
        ShowMenu = 0,

        /// <summary>Start the scene's mode at once, with the default loadout and no profile (a quick look in the Editor).</summary>
        StartMode = 1,

        /// <summary>Compose the scene and wait for the caller to prepare a match: tests and tools. No menu, no profile, no files touched.</summary>
        WaitForCaller = 2,
    }
}
