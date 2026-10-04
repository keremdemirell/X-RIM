using System;
using UnityEngine;
using XRim.Core;
using XRim.Input;
using XRim.Networking;
using XRim.Presentation.UI;
using XRim.Rules;
using XRim.Rules.Match;
using XRim.Rules.Paths;
using XRim.Rules.Planning;
using XRim.Rules.Settings;

namespace XRim.App
{
    /// <summary>
    /// Connects the planning HUD to one human's <see cref="TouchPlanSource"/> and to the authority (GDD §3, §6, §4). Each frame it
    /// shows what the rules allow right now: the countdown and whether the weapon switch is locked (the final
    /// <see cref="MatchSettings.WeaponSwitchLockoutSeconds"/>, a status effect, or Ready freezing the plan), whether Ready can still
    /// be taken back (D9, flagged), the weapon and body move set so far, the ink left (D7) and the opponent's public state: weapon
    /// and Ready, nothing else. What the player presses goes out as the same planning commands as the touches, so the rules decide;
    /// every refusal shows as a short note. Plain data in, plain data out: no rule lives here.
    /// <see cref="Source"/> is the human the HUD serves; it can change (the sandbox's hot-seat toggle).
    /// </summary>
    public sealed class PlanningHudPresenter : IDisposable
    {
        private const int PercentSteps = 100;

        private readonly IPlanningHudView _hud;
        private readonly ITurnAuthority _authority;
        private readonly MatchSetup _setup;
        private readonly MatchSettings _match;
        private readonly PerSide<PublicPlanningState?> _publicStates = new PerSide<PublicPlanningState?>(null, null);

        private TouchPlanSource _source;
        private TouchPlanSource _shownSource;
        private PlanningWindow _shownWindow;
        private int _shownRejectionCount;
        private bool? _shownMirrored;
        private float _shownBodyZoneFraction;
        private WeaponId? _shownWeapon;
        private BodyMove? _shownMove;
        private bool? _shownLocked;
        private string _shownLockReason;
        private ReadyButtonState? _shownReady;
        private int _shownInkPercent = -1;
        private PublicPlanningState? _shownOpponent;

        public PlanningHudPresenter(IPlanningHudView hud, ITurnAuthority authority, MatchSetup setup, MatchSettings match)
        {
            _hud = Guard.NotNull(hud, nameof(hud));
            _authority = Guard.NotNull(authority, nameof(authority));
            _setup = Guard.NotNull(setup, nameof(setup));
            _match = Guard.NotNull(match, nameof(match));
            _hud.WeaponChosen += OnWeaponChosen;
            _hud.ReadyPressed += OnReadyPressed;
            _authority.PublicStateChanged += OnPublicStateChanged;
            _authority.PlanningLocked += OnPlanningLocked;
        }

        /// <summary>The human the HUD serves; null hides it.</summary>
        public TouchPlanSource Source
        {
            get => _source;
            set => _source = value;
        }

        public void Dispose()
        {
            _hud.WeaponChosen -= OnWeaponChosen;
            _hud.ReadyPressed -= OnReadyPressed;
            _authority.PublicStateChanged -= OnPublicStateChanged;
            _authority.PlanningLocked -= OnPlanningLocked;
        }

        /// <param name="nowSeconds">The authority's clock, the one the planning deadline is on.</param>
        /// <param name="preview">The live preview of the stroke being drawn or last set, for the ink; null counts as all the ink left.</param>
        public void Tick(double nowSeconds, PathPreview preview)
        {
            TouchPlanSource source = _source;
            if (source == null || !source.IsPlanning)
            {
                _hud.SetVisible(false);
                return;
            }

            _hud.SetVisible(true);
            PlanningWindow window = source.Window;
            PlanDraft draft = source.Draft;
            if (!ReferenceEquals(window, _shownWindow) || !ReferenceEquals(source, _shownSource)) BeginWindow(source, window);

            ShowLayout(source.Layout);
            bool inLockout = PlanningTiming.IsInLockoutWindow(nowSeconds, window.DeadlineSeconds, _match.WeaponSwitchLockoutSeconds);
            _hud.ShowCountdown(window.DeadlineSeconds - nowSeconds, inLockout);
            ShowSelection(draft);
            ShowSwitchLock(window.Constraints[source.Side], draft, inLockout);
            ShowReady(draft, inLockout);
            ShowInk(preview);
            ShowOpponent(source.Side.Opponent(), window);
            ShowRefusals(draft);
        }

        /// <summary>A new planning phase (or a different human): the loadout, and nothing carried over from before.</summary>
        private void BeginWindow(TouchPlanSource source, PlanningWindow window)
        {
            _shownWindow = window;
            _shownSource = source;
            _shownRejectionCount = source.Draft.RejectionCount;
            _shownWeapon = null;
            _shownMove = null;
            _shownLocked = null;
            _shownLockReason = null;
            _shownReady = null;
            _shownInkPercent = -1;
            _shownOpponent = null;
            _shownMirrored = null;
            _hud.SetLoadout(_setup.Fighters[source.Side].Loadout);
            _hud.ShowMessage(string.Empty);
        }

        private void ShowLayout(ScreenLayout layout)
        {
            if (_shownMirrored == layout.IsMirrored && Mathf.Approximately(_shownBodyZoneFraction, layout.BodyZoneWidthFraction)) return;
            _shownMirrored = layout.IsMirrored;
            _shownBodyZoneFraction = layout.BodyZoneWidthFraction;
            _hud.ShowLayout(layout.IsMirrored, layout.BodyZoneWidthFraction);
        }

        private void ShowSelection(PlanDraft draft)
        {
            if (_shownWeapon != draft.Weapon)
            {
                _shownWeapon = draft.Weapon;
                _hud.ShowSelectedWeapon(draft.Weapon);
            }

            if (_shownMove != draft.BodyMove)
            {
                _shownMove = draft.BodyMove;
                _hud.ShowBodyMove(draft.BodyMove);
            }
        }

        /// <summary>Switching is disabled by a status effect, by the final lock-out (GDD §6, Decided), or while Ready freezes the plan.</summary>
        private void ShowSwitchLock(PlanningConstraints constraints, PlanDraft draft, bool inLockout)
        {
            string reason = !constraints.CanSwitchWeapon ? "WEAPONS DISABLED THIS TURN"
                : inLockout ? "WEAPONS LOCKED"
                : draft.IsReady ? "READY: CANCEL TO CHANGE"
                : null;
            bool locked = reason != null;
            if (_shownLocked == locked && _shownLockReason == reason) return;

            _shownLocked = locked;
            _shownLockReason = reason;
            _hud.ShowSwitchLocked(locked, reason ?? string.Empty);
        }

        /// <summary>D9 (flagged): Ready can be taken back until the lock-out starts; both halves are settings.</summary>
        private void ShowReady(PlanDraft draft, bool inLockout)
        {
            ReadyButtonState state = ReadyButtonState.Ready;
            if (draft.IsReady)
            {
                bool cancelAllowed = _match.AllowReadyCancel && (_match.AllowReadyCancelDuringLockout || !inLockout);
                state = cancelAllowed ? ReadyButtonState.Cancel : ReadyButtonState.LockedIn;
            }

            if (_shownReady == state) return;
            _shownReady = state;
            _hud.ShowReady(state);
        }

        private void ShowInk(PathPreview preview)
        {
            float fraction = preview != null ? preview.InkRemainingFraction : 1f;
            int percent = Mathf.RoundToInt(Mathf.Clamp01(fraction) * PercentSteps);
            if (percent == _shownInkPercent) return;
            _shownInkPercent = percent;
            _hud.ShowInkRemaining(fraction);
        }

        /// <summary>Until the opponent changes anything this turn, what everyone sees: the weapon in hand, not Ready.</summary>
        private void ShowOpponent(Side opponent, PlanningWindow window)
        {
            PublicPlanningState state = _publicStates[opponent] ??
                                        new PublicPlanningState(window.Board.State.Fighters[opponent].CurrentWeapon, false, false);
            if (_shownOpponent == state) return;
            _shownOpponent = state;
            _hud.ShowOpponent(state);
        }

        private void ShowRefusals(PlanDraft draft)
        {
            if (draft.RejectionCount == _shownRejectionCount) return;
            _shownRejectionCount = draft.RejectionCount;
            _hud.ShowMessage(RejectionMessages.Text(draft.LastRejection, _match.WeaponSwitchLockoutSeconds));
        }

        private void OnWeaponChosen(WeaponId weapon) => _source?.Send(new SelectWeaponCommand(weapon));

        private void OnReadyPressed()
        {
            if (_source == null) return;
            _source.Send(new SetReadyCommand(!_source.Draft.IsReady));
        }

        private void OnPublicStateChanged(Side side, PublicPlanningState state) => _publicStates[side] = state;

        /// <summary>The next turn starts from the board again, so what was seen of the opponent is forgotten.</summary>
        private void OnPlanningLocked(int turnIndex)
        {
            _publicStates.Left = null;
            _publicStates.Right = null;
        }
    }
}
