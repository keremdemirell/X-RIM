using System;
using XRim.Core.Gdd;
using XRim.Rules.SuddenDeath;

namespace XRim.Rules.Settings
{
    /// <summary>
    /// Sudden death (GDD §14). Triggers, 1 HP (<see cref="RuleConstants.SuddenDeathHp"/>), first valid hit wins and the time-to-impact
    /// tie-break are Decided. The options below are TBD, on the designer's defaults (2026-10-11: "all recommended").
    /// </summary>
    [Serializable]
    public sealed class SuddenDeathSettings
    {
        /// <summary>
        /// Carry over (P5): positions, poses, limb damage, lost and floor limbs, a held club and a pending stun stay (a split torso is
        /// rebuilt whole); reset: the start lines and whole, undamaged dummies with no floor limbs, club or statuses.
        /// </summary>
        [GddTbd("§14", "Sudden death board state", Proposal = "The current board carries over; designer 2026-10-11: the recommended default")]
        public SuddenDeathBoardMode BoardMode = SuddenDeathBoardMode.CarryOver;

        /// <summary>
        /// How many times a sudden-death turn without a winner is played again (no hit, a tie replay, both idle: P6) before
        /// <see cref="ISuddenDeathNoHitPolicy"/>'s fallback decides. 0: the first such turn already goes to the fallback.
        /// </summary>
        [GddTbd("§14", "Nobody hits in sudden death",
            Proposal = "Repeat at most 3 times, then more HP when sudden death began wins (equal: a coin toss); designer 2026-10-11")]
        public int MaxRepeatsWithoutHit = 3;

        [GddTbd("§14", "Options in sudden death", Proposal = "Weapon switching and signature moves stay available; designer 2026-10-11")]
        public bool AllowWeaponSwitching = true;

        [GddTbd("§14", "Options in sudden death", Proposal = "Weapon switching and signature moves stay available; designer 2026-10-11")]
        public bool AllowSignatureMoves = true;
    }
}
