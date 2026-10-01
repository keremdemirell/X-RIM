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

        /// <summary>
        /// D9 (not decided; recommended default built as a flag): Ready can be cancelled to edit again. The opponent
        /// sees Ready (Decided), so a fake Ready is a bluff (pillar 2).
        /// </summary>
        [GddTbd("§3", "Can a player cancel Ready to edit?", Proposal = "Allowed until the lock-out starts")]
        public bool AllowReadyCancel = true;

        /// <summary>D9: whether Ready can still be cancelled in the final <see cref="WeaponSwitchLockoutSeconds"/>.</summary>
        [GddTbd("§3", "Can Ready be cancelled in the final lock-out seconds?", Proposal = "No: the cancel window closes when the weapon lock-out starts")]
        public bool AllowReadyCancelDuringLockout = false;

        [GddTbd("§6", "Drawing during the weapon lock-out", Proposal = "Allowed")]
        public bool AllowDrawingDuringLockout = true;
    }
}
