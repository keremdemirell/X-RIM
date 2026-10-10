using System;
using XRim.Core;
using XRim.Input;
using XRim.Networking;
using XRim.Rules.Planning;

namespace XRim.App
{
    /// <summary>
    /// Hot-seat (D25, a developer mode, not a GDD rule): two people share one device. One screen cannot plan simultaneously, so the
    /// players plan one after the other, each alone with the full planning time and the real lock-out, and a cover screen between
    /// them keeps each plan secret (designer, 2026-10-04):
    /// <list type="bullet">
    /// <item>In this mode the coordinator drives the authority's clock (<see cref="Advance"/> replaces the usual clock tick). Behind
    /// the cover screen it stands still. When the second player's turn comes it is turned back to the moment planning opened, so both
    /// players get the same window and the authority enforces the same deadline and lock-out on each.</item>
    /// <item>Ready ends a player's slot, so Ready cannot be taken back in hot-seat. When the first player's time runs out, the slot
    /// ends just before their deadline with whatever is set, and Ready is pressed for them so the turn can lock once the second
    /// player is done. The second player's time running out locks the turn as usual.</item>
    /// <item>The first planner alternates each turn, because the second one sees the first's final weapon and Ready (public, GDD §6).
    /// A side with less planning time (a stun under D17's "shorter planning" option) goes first, so the clock never passes its
    /// deadline while the other side plans.</item>
    /// </list>
    /// Between planning phases (execution, playback, match over) the clock simply runs.
    /// </summary>
    public sealed class HotSeatCoordinator : IDisposable
    {
        /// <summary>The first slot ends this long before that player's deadline, so planning is still open for their Ready (technical).</summary>
        public const double SlotEndMarginSeconds = 0.001;

        private readonly ITurnAuthority _authority;
        private readonly ManualClock _clock;
        private readonly PerSide<TouchPlanSource> _players;
        private PlanningWindow _window;
        private double _openedSeconds;

        /// <param name="clock">The authority's clock; this coordinator advances it while the match is in hot-seat.</param>
        /// <param name="players">The two humans, one per side.</param>
        public HotSeatCoordinator(ITurnAuthority authority, ManualClock clock, PerSide<TouchPlanSource> players)
        {
            _authority = Guard.NotNull(authority, nameof(authority));
            _clock = Guard.NotNull(clock, nameof(clock));
            _players = Guard.NotNull(players, nameof(players));
            if (players.Left.Side != Side.Left || players.Right.Side != Side.Right)
            {
                throw new ArgumentException("Each player must play their own side.", nameof(players));
            }

            _authority.PlanningStarted += OnPlanningStarted;
            _authority.PlanningLocked += OnPlanningLocked;
            DeactivateBoth();
        }

        public HotSeatStage Stage { get; private set; } = HotSeatStage.Idle;

        /// <summary>The player on the cover screen or planning (meaningless while <see cref="Stage"/> is Idle).</summary>
        public Side ActiveSide { get; private set; }

        /// <summary>True once the first player of this turn is done: the player up now is the second.</summary>
        public bool IsSecondSlot { get; private set; }

        /// <summary>The player up now; null while <see cref="Stage"/> is Idle.</summary>
        public TouchPlanSource ActivePlayer => Stage == HotSeatStage.Idle ? null : _players[ActiveSide];

        public void Dispose()
        {
            _authority.PlanningStarted -= OnPlanningStarted;
            _authority.PlanningLocked -= OnPlanningLocked;
        }

        /// <summary>The player on the cover screen has taken the device: their planning starts and the timer runs.</summary>
        public void ConfirmCover()
        {
            if (Stage != HotSeatStage.Cover) return;
            Stage = HotSeatStage.Planning;
            _players[ActiveSide].IsActive = true;
        }

        /// <summary>Call once per frame instead of advancing the authority's clock: moves it by real time unless the cover stops it.</summary>
        public void Advance(double realDeltaSeconds)
        {
            switch (Stage)
            {
                case HotSeatStage.Cover:
                    return; // the planning timer stands still while the device changes hands
                case HotSeatStage.Planning when !IsSecondSlot:
                    AdvanceFirstSlot(realDeltaSeconds);
                    return;
                default:
                    _clock.Advance(realDeltaSeconds);
                    return;
            }
        }

        /// <summary>
        /// Who plans first this turn: the side with less planning time, otherwise Left on even turns and Right on odd ones.
        /// </summary>
        public static Side FirstPlanner(PlanningWindow window)
        {
            Guard.NotNull(window, nameof(window));
            float left = window.Constraints[Side.Left].PlanningDurationSeconds;
            float right = window.Constraints[Side.Right].PlanningDurationSeconds;
            if (left < right) return Side.Left;
            if (right < left) return Side.Right;
            return window.TurnIndex % 2 == 0 ? Side.Left : Side.Right;
        }

        private void AdvanceFirstSlot(double realDeltaSeconds)
        {
            if (_players[ActiveSide].Draft.IsReady)
            {
                EndFirstSlot();
                return;
            }

            double slotEnd = DeadlineOf(ActiveSide) - SlotEndMarginSeconds;
            double target = Math.Min(_clock.NowSeconds + realDeltaSeconds, slotEnd);
            if (target > _clock.NowSeconds) _clock.Set(target);
            if (_clock.NowSeconds >= slotEnd) EndFirstSlot();
        }

        private void EndFirstSlot()
        {
            TouchPlanSource player = _players[ActiveSide];

            // Time is up: whatever is set executes (GDD §3). Ready lets the turn lock as soon as the second player is done.
            if (!player.Draft.IsReady) player.Send(new SetReadyCommand(true));
            player.IsActive = false; // a stroke still being drawn is dropped, as when the timer ends

            _clock.Set(_openedSeconds);
            ActiveSide = ActiveSide.Opponent();
            IsSecondSlot = true;
            Stage = HotSeatStage.Cover;
        }

        /// <summary>Each side's own deadline, exactly as its planning session computed it.</summary>
        private double DeadlineOf(Side side) => _openedSeconds + _window.Constraints[side].PlanningDurationSeconds;

        private void OnPlanningStarted(PlanningWindow window)
        {
            // Raised as planning opens, before the clock moves: this is the moment both sessions measure their deadlines from.
            _window = window;
            _openedSeconds = _clock.NowSeconds;
            ActiveSide = FirstPlanner(window);
            IsSecondSlot = false;
            Stage = HotSeatStage.Cover;
            DeactivateBoth();
        }

        private void OnPlanningLocked(int turnIndex)
        {
            Stage = HotSeatStage.Idle;
            DeactivateBoth();
        }

        private void DeactivateBoth()
        {
            _players.Left.IsActive = false;
            _players.Right.IsActive = false;
        }
    }
}
