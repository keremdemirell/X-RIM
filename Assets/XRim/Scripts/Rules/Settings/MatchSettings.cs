using System;
using XRim.Core.Gdd;

namespace XRim.Rules.Settings
{
    /// <summary>Match and turn flow tuning (GDD §3, §6, §9, §13).</summary>
    [Serializable]
    public sealed class MatchSettings
    {
        /// <summary>GDD §3, Tunable. Appendix A starting range: 10–12 s.</summary>
        public float PlanningDurationSeconds = 12f;

        /// <summary>
        /// GDD §3/§13: the cap rule is Decided, the value is Tunable. The designer confirmed 30 on
        /// 2026-09-29; Appendix A's "15 turns" is outdated.
        /// </summary>
        public int TurnCap = 30;

        /// <summary>GDD §3/§9, Tunable. Execution also ends early once both paths finish and physics settles.</summary>
        public float ExecutionHardCapSeconds = 1.5f;

        /// <summary>GDD §6, Tunable: weapon switching is disabled in the final N seconds of planning.</summary>
        public float WeaponSwitchLockoutSeconds = 1.5f;

        [GddTbd("§3", "Can a player cancel Ready to edit?")]
        public bool AllowReadyCancel = false;

        [GddTbd("§6", "Drawing during the weapon lock-out", Proposal = "Allowed")]
        public bool AllowDrawingDuringLockout = true;
    }
}
