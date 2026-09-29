using XRim.Core.Gdd;

namespace XRim.Rules.SuddenDeath
{
    [GddTbd("§14", "Sudden death board state")]
    public enum SuddenDeathBoardMode
    {
        /// <summary>Positions, poses and limb damage carry over from the last turn.</summary>
        CarryOver = 0,

        /// <summary>Positions and limbs reset to the match start.</summary>
        ResetToMatchStart = 1,
    }
}
