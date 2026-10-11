using System;
using System.Collections.Generic;
using XRim.Core;
using XRim.Core.Gdd;
using XRim.Rules.Arena;
using XRim.Rules.Match;
using XRim.Rules.Settings;

namespace XRim.Rules.SuddenDeath
{
    /// <summary>
    /// The rules half of sudden death's setup (GDD §14): the state is marked as sudden death and both dummies are set to
    /// <see cref="RuleConstants.SuddenDeathHp"/> (Decided). What the designer chose for the TBD parts (2026-10-11):
    /// <list type="bullet">
    /// <item>the HP each had is kept for the no-hit fallback (<see cref="FighterState.HpBeforeSuddenDeath"/>);</item>
    /// <item>P5, the board: carried over by default; with <see cref="SuddenDeathBoardMode.ResetToMatchStart"/> every limb is whole and
    /// undamaged again, no club is held, no weapon is lost, no status remains, and a club or body blow gives way to the first loadout
    /// weapon (the simulation puts the dummies back on their start lines);</item>
    /// <item>the electric walls go down unless they stay live in sudden death (<see cref="ElectricWallSettings.ActiveInSuddenDeath"/>, off),
    /// and always on a reset;</item>
    /// <item>idle counters restart, so the forfeit rule begins afresh (not in the GDD).</item>
    /// </list>
    /// A sudden-death turn without a winner is played again through <see cref="Repeat"/> (P6).
    /// </summary>
    [GddTbd("§14", "Sudden death setup beyond 1 HP: board state, electric wall",
        Proposal = "The board carries over, walls off (designer 2026-10-11)")]
    public sealed class SuddenDeathSetup
    {
        public void Apply(MatchState state, RulesSettings settings, MatchSetup setup)
        {
            Guard.NotNull(state, nameof(state));
            Guard.NotNull(settings, nameof(settings));
            Guard.NotNull(setup, nameof(setup));

            bool reset = settings.SuddenDeath.BoardMode == SuddenDeathBoardMode.ResetToMatchStart;
            bool wallsStay = !reset && ElectricWallRules.AreLive(true, settings.ElectricWall);
            state.IsSuddenDeath = true;
            state.SuddenDeathRepeats = 0;
            foreach (Side side in new[] { Side.Left, Side.Right })
            {
                FighterState fighter = state.Fighters[side];
                fighter.HpBeforeSuddenDeath = Math.Max(0f, fighter.Hp);
                if (reset) Reset(fighter, setup.Fighters[side].Loadout);
                fighter.Hp = RuleConstants.SuddenDeathHp;
                fighter.ConsecutiveIdleTurns = 0;
                if (!wallsStay) state.Walls[side].Clear();
            }
        }

        /// <summary>
        /// A sudden-death turn without a winner is played again (P6): one more repeat counted, both at 1 HP (after a tie both fell), the board
        /// as it ended. Idle counters keep counting.
        /// </summary>
        public void Repeat(MatchState state)
        {
            Guard.NotNull(state, nameof(state));
            state.SuddenDeathRepeats++;
            state.Fighters[Side.Left].Hp = RuleConstants.SuddenDeathHp;
            state.Fighters[Side.Right].Hp = RuleConstants.SuddenDeathHp;
        }

        private static void Reset(FighterState fighter, IReadOnlyList<WeaponId> loadout)
        {
            fighter.RestoreLimbs();
            fighter.HeldLimb = null;
            fighter.LostWeapons.Clear();
            fighter.Statuses.Clear();
            if (loadout.Count > 0 && !Contains(loadout, fighter.CurrentWeapon)) fighter.CurrentWeapon = loadout[0];
        }

        private static bool Contains(IReadOnlyList<WeaponId> loadout, WeaponId weapon)
        {
            for (int i = 0; i < loadout.Count; i++)
            {
                if (loadout[i] == weapon) return true;
            }

            return false;
        }
    }
}
