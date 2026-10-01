using XRim.Core;
using XRim.Core.Gdd;
using XRim.Rules.Match;

namespace XRim.Rules.SuddenDeath
{
    /// <summary>
    /// The transition into sudden death (GDD §14): the match state is marked as sudden death and both dummies are set
    /// to <see cref="RuleConstants.SuddenDeathHp"/> (Decided). Everything else is Session 12's: whether positions and
    /// limbs reset to the match start (<c>SuddenDeathSettings.BoardMode</c>, TBD; until then the board always carries
    /// over), the electric wall, the available options, and the first-hit rules.
    /// Idle counters restart, so the forfeit rule begins afresh for the sudden-death turn (not in the GDD).
    /// </summary>
    [GddTbd("§14", "Sudden death setup beyond 1 HP: board state, electric wall, available options", Proposal = "The current board carries over")]
    public sealed class SuddenDeathSetup
    {
        public void Apply(MatchState state)
        {
            Guard.NotNull(state, nameof(state));

            state.IsSuddenDeath = true;
            foreach (Side side in new[] { Side.Left, Side.Right })
            {
                FighterState fighter = state.Fighters[side];
                fighter.Hp = RuleConstants.SuddenDeathHp;
                fighter.ConsecutiveIdleTurns = 0;
            }
        }
    }
}
