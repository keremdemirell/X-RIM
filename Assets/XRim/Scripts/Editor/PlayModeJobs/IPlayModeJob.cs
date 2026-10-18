namespace XRim.Editor.PlayModeJobs
{
    /// <summary>
    /// Work that needs the hidden physics world (it only exists in Play mode) but is started from an Editor menu: writing the seed scenario
    /// fixtures, a bot-vs-bot balance batch. <see cref="PlayModeJobRunner"/> enters Play mode in an empty scene, calls <see cref="Step"/> until
    /// it returns false (a slice at a time, so the Editor stays responsive and a progress bar can offer Cancel), then <see cref="Finish"/>.
    /// </summary>
    internal interface IPlayModeJob
    {
        /// <summary>The progress bar's and closing dialog's title.</summary>
        string Title { get; }

        /// <summary>What the job is doing now, for the progress bar.</summary>
        string Status { get; }

        /// <summary>From 0 to 1.</summary>
        float Progress { get; }

        /// <summary>One small piece of work (a turn, a scenario); false once there is nothing left to do.</summary>
        bool Step();

        /// <summary>
        /// Called once, in Play mode, after the last step, a cancel or a failure: writes what the job produced and returns the message the
        /// closing dialog shows.
        /// </summary>
        string Finish(bool cancelled);
    }
}
