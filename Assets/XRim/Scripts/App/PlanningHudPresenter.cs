using System;
using System.Collections.Generic;
using System.Globalization;
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
using XRim.Rules.Signature;

namespace XRim.App
{
    /// <summary>
    /// Connects the planning HUD to one human's <see cref="TouchPlanSource"/> and to the authority (GDD §3, §6, §4). Each frame it
    /// shows what the rules allow right now: the countdown and whether the weapon switch is locked (the final
    /// <see cref="MatchSettings.WeaponSwitchLockoutSeconds"/>, a status effect, or Ready freezing the plan), whether Ready can still
    /// be taken back (D9, flagged), the weapon and body move set so far, the ink left (D7) and the opponent's public state: weapon
    /// and Ready, nothing else. What the player presses goes out as the same planning commands as the touches, so the rules decide;
    /// every refusal shows as a short note. Plain data in, plain data out: no rule lives here.
    /// The signature buttons (GDD §8) show the moves the player brought and their charge, as the authority's window says; a tap sets
    /// a move (its pattern becomes the path and the preview), a tap on the move already set cancels it (as clearing the path does).
    /// <see cref="Source"/> is the human the HUD serves; it can change (the sandbox's hot-seat toggle).
    /// </summary>
    public sealed class PlanningHudPresenter : IDisposable
    {
        private const int PercentSteps = 100;

        /// <summary>Shown as planning opens while the player's own electric wall stands behind their dummy (GDD §13).</summary>
        public const string WallBehindYouMessage = "ELECTRIC WALL BEHIND YOU: STEP BACK AGAIN AND IT ZAPS";

        /// <summary>The signature charge words (§8): a move can be used, the charge is there but moves are off this turn (§14), filling, empty or spent.</summary>
        public const string SignatureReadyText = "READY";
        public const string SignatureOffThisTurnText = "NOT THIS TURN";
        public const string SignatureNotChargedText = "NOT CHARGED";
        public const string SignatureChargingText = "CHARGING ";

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
        private readonly List<SignatureButtonState> _signatureButtons = new List<SignatureButtonState>();
        private readonly List<SignatureButtonState> _shownSignatureButtons = new List<SignatureButtonState>();
        private float _shownSignatureCharge = -1f;
        private string _shownSignatureText;

        public PlanningHudPresenter(IPlanningHudView hud, ITurnAuthority authority, MatchSetup setup, MatchSettings match)
        {
            _hud = Guard.NotNull(hud, nameof(hud));
            _authority = Guard.NotNull(authority, nameof(authority));
            _setup = Guard.NotNull(setup, nameof(setup));
            _match = Guard.NotNull(match, nameof(match));
            _hud.WeaponChosen += OnWeaponChosen;
            _hud.ReadyPressed += OnReadyPressed;
            _hud.SignatureChosen += OnSignatureChosen;
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
            _hud.SignatureChosen -= OnSignatureChosen;
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
            ShowSignatures(window.Signatures[source.Side], draft);
            ShowRefusals(draft);
        }

        /// <summary>
        /// A new planning phase (or a different human): the weapons that can be held this turn (the loadout and the §12 extra options:
        /// the severed-limb club, the armless body blow), nothing carried over from before, and a warning when the player's own electric
        /// wall stands behind them (§13: one more step back is a zap).
        /// </summary>
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
            _shownSignatureButtons.Clear();
            _shownSignatureCharge = -1f;
            _shownSignatureText = null;
            _hud.SetLoadout(window.Constraints[source.Side].AvailableWeapons(_setup.Fighters[source.Side].Loadout));
            _hud.ShowMessage(window.Board.State.Walls[source.Side].IsUp ? WallBehindYouMessage : string.Empty);
        }

        private void ShowLayout(ScreenLayout layout)
        {
            if (_shownMirrored == layout.IsMirrored && Mathf.Approximately(_shownBodyZoneFraction, layout.EffectiveBodyZoneFraction)) return;
            _shownMirrored = layout.IsMirrored;
            _shownBodyZoneFraction = layout.EffectiveBodyZoneFraction;
            _hud.ShowLayout(layout.IsMirrored, layout.EffectiveBodyZoneFraction, layout.BodyZoneWidthFraction);
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

        /// <summary>
        /// One button per move the player brought (§8): usable when the window says a move can be used, the move works with the weapon in
        /// hand and Ready does not freeze the plan; set when the draft holds it. The charge fill and its word are shared by all buttons.
        /// </summary>
        private void ShowSignatures(SignatureOptions options, PlanDraft draft)
        {
            _signatureButtons.Clear();
            foreach (SignatureMoveStats move in options.Moves)
            {
                bool usable = options.CanUse && move.AllowsWeapon(draft.Weapon) && !draft.IsReady;
                _signatureButtons.Add(new SignatureButtonState(move.MoveId, move.DisplayName, usable, draft.Signature == move.MoveId));
            }

            float charge = Mathf.Clamp01(options.Charge.ChargeFraction);
            string text = ChargeText(options);
            if (SameButtons(_signatureButtons, _shownSignatureButtons) && Mathf.Approximately(charge, _shownSignatureCharge) &&
                text == _shownSignatureText) return;

            _shownSignatureButtons.Clear();
            _shownSignatureButtons.AddRange(_signatureButtons);
            _shownSignatureCharge = charge;
            _shownSignatureText = text;
            _hud.ShowSignatures(_shownSignatureButtons.ToArray(), charge, text);
        }

        /// <summary>The charge in a word: ready, charged but off this turn (§14), filling up (the bar), or not charged (empty, or the once-per-match move spent).</summary>
        public static string ChargeText(SignatureOptions options)
        {
            Guard.NotNull(options, nameof(options));
            if (options.CanUse) return SignatureReadyText;
            if (options.Charge.IsAvailable) return SignatureOffThisTurnText;
            float charge = Mathf.Clamp01(options.Charge.ChargeFraction);
            if (charge <= 0f) return SignatureNotChargedText;
            return SignatureChargingText + Mathf.FloorToInt(charge * PercentSteps).ToString(CultureInfo.InvariantCulture) + "%";
        }

        private static bool SameButtons(List<SignatureButtonState> a, List<SignatureButtonState> b)
        {
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++)
            {
                if (!a[i].Equals(b[i])) return false;
            }

            return true;
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

        /// <summary>A tap sets the move; a tap on the move already set cancels it, which is clearing the path (§8: its pattern is the path).</summary>
        private void OnSignatureChosen(SignatureMoveId move)
        {
            if (_source == null) return;
            if (_source.Draft.Signature == move) _source.Send(new ClearPathCommand());
            else _source.Send(new UseSignatureCommand(move));
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
