using System.Collections.Generic;
using XRim.Core;
using XRim.Rules;
using XRim.Rules.Match;
using XRim.Simulation.Recording;

namespace XRim.Networking.Server
{
    /// <summary>
    /// What one player may see of the match (GDD §3, §8; designer 2026-10-25, Q3). Body moves, paths and the chosen signature move never
    /// leave the server during planning: the messages simply do not carry them. What the board and setup would also give away is blanked
    /// here: the opponent's signature charge and uses (unless <c>SignatureSettings.OpponentSeesCharged</c>), the moves the opponent brought,
    /// and the match seed (it decides the sudden-death coin toss). A turn's result reveals the rest as it plays.
    /// </summary>
    internal static class ClientView
    {
        public static MatchSetup Setup(MatchSetup setup, Side viewer)
        {
            Side opponent = viewer.Opponent();
            FighterSetup hidden = setup.Fighters[opponent];
            var fighters = new PerSide<FighterSetup>(setup.Fighters.Left, setup.Fighters.Right)
            {
                [opponent] = new FighterSetup(hidden.Handedness, hidden.Loadout, new List<SignatureMoveId>()),
            };
            return new MatchSetup(fighters, 0u);
        }

        public static BoardSnapshot Board(BoardSnapshot board, Side viewer, bool opponentSeesCharge)
        {
            BoardSnapshot view = board.Clone();
            if (!opponentSeesCharge) HideCharge(view.State, viewer.Opponent());
            return view;
        }

        public static TurnResult Result(TurnResult result, Side viewer, bool opponentSeesCharge)
        {
            if (opponentSeesCharge) return result;

            MatchState resolved = result.Report.ResolvedState.Clone();
            HideCharge(resolved, viewer.Opponent());
            var report = new ExecutionReport(resolved, result.Report.FirstValidHitTime, result.Report.FirstHitsOnSameStep, result.Report.DeathTime);
            return new TurnResult(result.TurnIndex, result.Timeline, Board(result.FinalBoard, viewer, false), report, result.EndReason);
        }

        private static void HideCharge(MatchState state, Side side)
        {
            FighterState fighter = state.Fighters[side];
            fighter.SignatureCharge = 0f;
            fighter.SignatureUses = 0;
        }
    }
}
