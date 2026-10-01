namespace XRim.Rules.Planning
{
    /// <summary>
    /// When the things that matter to the timing rules last happened, on the authority's clock (GDD §18: the server
    /// checks the weapon lock-out and the Ready time against its own clock). <see cref="PlanningSession"/> records
    /// it as commands arrive; <see cref="PlanValidator"/> re-checks it when the plan locks.
    /// </summary>
    public readonly struct PlanningAudit
    {
        public double DeadlineSeconds { get; }

        /// <summary>When the weapon last changed, or null when it was never switched this turn.</summary>
        public double? LastWeaponSwitchSeconds { get; }

        /// <summary>When the path was last drawn or cleared, or null when it was never touched this turn.</summary>
        public double? LastPathChangeSeconds { get; }

        /// <summary>When Ready was pressed, or null when it is not pressed (or was cancelled).</summary>
        public double? ReadySeconds { get; }

        public PlanningAudit(double deadlineSeconds, double? lastWeaponSwitchSeconds, double? lastPathChangeSeconds,
            double? readySeconds)
        {
            DeadlineSeconds = deadlineSeconds;
            LastWeaponSwitchSeconds = lastWeaponSwitchSeconds;
            LastPathChangeSeconds = lastPathChangeSeconds;
            ReadySeconds = readySeconds;
        }
    }
}
