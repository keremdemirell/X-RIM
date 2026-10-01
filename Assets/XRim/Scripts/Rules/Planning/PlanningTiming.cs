namespace XRim.Rules.Planning
{
    /// <summary>
    /// The planning clock rules in one place, so <see cref="PlanningSession"/> (as commands arrive) and
    /// <see cref="PlanValidator"/> (when the plan locks) cannot disagree about a boundary.
    /// </summary>
    public static class PlanningTiming
    {
        /// <summary>Planning is open until the deadline; from the deadline on, input no longer counts (GDD §3).</summary>
        public static bool IsOpen(double nowSeconds, double deadlineSeconds) => nowSeconds < deadlineSeconds;

        /// <summary>
        /// True in the final <paramref name="lockoutSeconds"/> of planning, boundary included: with exactly 1.5 s left a
        /// weapon switch is already refused (GDD §6).
        /// </summary>
        public static bool IsInLockoutWindow(double nowSeconds, double deadlineSeconds, float lockoutSeconds) =>
            nowSeconds >= deadlineSeconds - lockoutSeconds;
    }
}
