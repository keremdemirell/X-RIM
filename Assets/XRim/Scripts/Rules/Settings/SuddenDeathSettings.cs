using System;
using XRim.Core.Gdd;
using XRim.Rules.SuddenDeath;

namespace XRim.Rules.Settings
{
    /// <summary>
    /// Sudden death (GDD §14). Triggers, 1 HP (<see cref="RuleConstants.SuddenDeathHp"/>), first valid hit
    /// wins and the time-to-impact tie-break are Decided. The options below are TBD.
    /// </summary>
    [Serializable]
    public sealed class SuddenDeathSettings
    {
        [GddTbd("§14", "Sudden death board state")]
        public SuddenDeathBoardMode BoardMode = SuddenDeathBoardMode.CarryOver;

        /// <summary>0 = repeat without limit.</summary>
        [GddTbd("§14", "Nobody hits in sudden death", Proposal = "Repeat the turn (may need its own cap)")]
        public int MaxTurnsWithoutHit = 0;

        [GddTbd("§14", "Options in sudden death", Proposal = "Weapon switching and signature moves stay available")]
        public bool AllowWeaponSwitching = true;

        [GddTbd("§14", "Options in sudden death", Proposal = "Weapon switching and signature moves stay available")]
        public bool AllowSignatureMoves = true;
    }
}
