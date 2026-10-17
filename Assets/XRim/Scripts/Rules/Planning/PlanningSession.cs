using System;
using System.Collections.Generic;
using XRim.Core;
using XRim.Rules.Paths;
using XRim.Rules.Settings;
using XRim.Rules.Signature;

namespace XRim.Rules.Planning
{
    /// <summary>
    /// One player's planning phase. Clients run it for instant feedback (ink meter, lock-out);
    /// the authority runs the same code to validate (GDD §18).
    /// <list type="bullet">
    /// <item>Weapon switch: only to a weapon the constraints allow this turn (the loadout, minus weapons out of hand, plus the §12
    /// extra options: the severed-limb club, the armless body blow), erases the drawn path, refused in the final
    /// <see cref="MatchSettings.WeaponSwitchLockoutSeconds"/> and public at once (§6).</item>
    /// <item>Path: built through <see cref="PathBuilder"/> (start, reach and ink rules, §6), so a stroke past the
    /// ink budget is cut where the ink runs out, never refused.</item>
    /// <item>Signature move (§8, TBD; designer 2026-10-15): its preset pattern becomes the path (<see cref="SignaturePathBuilder"/>),
    /// when the move is brought, the dummy is charged, the weapon is one it works with and signature moves are allowed this turn
    /// (<see cref="PlanningConstraints.CanUseSignature"/>). It replaces only the weapon path: a body swipe stays unless the move
    /// defines its own. Drawing, clearing or switching weapon cancels it. Nothing is spent here: the charge is spent when the plan
    /// locks, so choosing and cancelling is free. It follows the drawing lock-out and stays hidden from the opponent.</item>
    /// <item>Ready, with cancel per <see cref="MatchSettings.AllowReadyCancel"/> (§3, TBD). While Ready the plan is frozen.</item>
    /// <item>Timeout: once the deadline passes, commands are refused and whatever is set still executes (§3, Decided).</item>
    /// </list>
    /// Every command carries the authority's clock reading, so a client cannot fake timing.
    /// </summary>
    public sealed class PlanningSession
    {
        private readonly IReadOnlyList<WeaponId> _loadout;
        private readonly IWeaponTipSource _weaponTips;
        private readonly RulePolicies _policies;
        private readonly PathBuilder _pathBuilder;
        private readonly SignaturePathBuilder _signaturePathBuilder;
        private readonly IReadOnlyList<SignatureMoveStats> _signatures;

        private WeaponId _weapon;
        private BodyMove _bodyMove = BodyMove.None;
        private WeaponPath _drawn = WeaponPath.Empty;
        private BuiltPath _builtPath;
        private SignatureMoveId _signature;
        private SignatureMoveStats _signatureMove;
        private double? _lastWeaponSwitchSeconds;
        private double? _lastPathChangeSeconds;
        private double? _readySeconds;

        public Side Side { get; }
        public PlanningConstraints Constraints { get; }
        public double DeadlineSeconds { get; }
        public bool IsReady { get; private set; }

        /// <summary>The weapon currently selected (public to the opponent at once).</summary>
        public WeaponId Weapon => _weapon;

        public IReadOnlyList<WeaponId> Loadout => _loadout;

        /// <summary>Every weapon that can be picked this turn: the available loadout weapons, then the extra options (§12).</summary>
        public IReadOnlyList<WeaponId> AvailableWeapons => Constraints.AvailableWeapons(_loadout);

        /// <summary>What <see cref="PathBuilder"/> made of the last accepted stroke, for the preview. Null when no path is set.</summary>
        public BuiltPath CurrentPath => _builtPath;

        /// <summary>Whether the opponent may see that a signature move is charged (GDD §8, TBD).</summary>
        public bool OpponentSeesChargedSignature => Settings.Signature.OpponentSeesCharged;

        private RulesSettings Settings { get; }

        /// <param name="signatureCharge">How charged the dummy is as planning opens (§8); not charged when left out.</param>
        /// <param name="signatures">The signature moves the dummy brought (§8); null: every move of the settings' catalogue.</param>
        public PlanningSession(Side side, WeaponId currentWeapon, IReadOnlyList<WeaponId> loadout,
            IWeaponTipSource weaponTips, PlanningConstraints constraints, double deadlineSeconds,
            RulesSettings settings, RulePolicies policies, SignatureChargeState signatureCharge = default,
            IReadOnlyList<SignatureMoveStats> signatures = null)
        {
            SignatureCharge = signatureCharge;
            Side = side;
            _weapon = currentWeapon;
            _loadout = Guard.NotNull(loadout, nameof(loadout));
            _weaponTips = Guard.NotNull(weaponTips, nameof(weaponTips));
            Constraints = Guard.NotNull(constraints, nameof(constraints));
            DeadlineSeconds = deadlineSeconds;
            Settings = Guard.NotNull(settings, nameof(settings));
            _policies = Guard.NotNull(policies, nameof(policies));
            _pathBuilder = new PathBuilder(_policies);
            _signaturePathBuilder = new SignaturePathBuilder(_policies);
            _signatures = signatures ?? SignatureCatalogue.Brought(settings, null);
        }

        /// <summary>
        /// Whatever is set right now. This is what executes when the timer ends, Ready or not (§3, Decided). A signature move that
        /// defines its own body move runs with it instead of the swipe (§8).
        /// </summary>
        public TurnPlan CurrentPlan =>
            new TurnPlan(_weapon, PlannedBodyMove, _builtPath != null ? _builtPath.Path : WeaponPath.Empty, _signature, IsReady);

        /// <summary>How charged the dummy was when planning opened (§8). Nothing changes it during planning.</summary>
        public SignatureChargeState SignatureCharge { get; }

        /// <summary>The signature moves the dummy brought (§8), for the HUD's buttons. Usable only when <see cref="CanUseSignature"/>.</summary>
        public IReadOnlyList<SignatureMoveStats> Signatures => _signatures;

        /// <summary>The signature move set so far; empty when none is (hidden from the opponent until execution, §8).</summary>
        public SignatureMoveId SelectedSignature => _signature;

        /// <summary>The weapon tips planning builds from (the preset's lead-in starts at the weapon in hand).</summary>
        public IWeaponTipSource WeaponTips => _weaponTips;

        /// <summary>
        /// A signature move can be used this turn as far as the dummy goes: it is charged, it brought a move and signature moves are
        /// allowed (sudden death can switch them off, §14). Also what "charged" means to the opponent's view.
        /// </summary>
        public bool CanUseSignature => Constraints.CanUseSignature && SignatureCharge.IsAvailable && _signatures.Count > 0;

        public PublicPlanningState PublicState => _policies.PublicState.Build(this, CanUseSignature);

        private BodyMove PlannedBodyMove =>
            _signatureMove != null && _signatureMove.OverridesBodyMove ? _signatureMove.BodyMoveOverride : _bodyMove;

        public PlanningAudit Audit =>
            new PlanningAudit(DeadlineSeconds, _lastWeaponSwitchSeconds, _lastPathChangeSeconds, _readySeconds);

        /// <summary>True until the deadline: from the deadline on, commands are refused.</summary>
        public bool IsOpen(double nowSeconds) => PlanningTiming.IsOpen(nowSeconds, DeadlineSeconds);

        /// <summary>
        /// True in the final <see cref="MatchSettings.WeaponSwitchLockoutSeconds"/> of planning, boundary included:
        /// at exactly 1.5 s left a switch is already refused (§6).
        /// </summary>
        public bool IsInLockoutWindow(double nowSeconds) =>
            PlanningTiming.IsInLockoutWindow(nowSeconds, DeadlineSeconds, Settings.Match.WeaponSwitchLockoutSeconds);

        public bool IsWeaponSwitchLockedOut(double nowSeconds) =>
            !Constraints.CanSwitchWeapon || IsInLockoutWindow(nowSeconds);

        public bool IsDrawingLockedOut(double nowSeconds) =>
            !Settings.Match.AllowDrawingDuringLockout && IsInLockoutWindow(nowSeconds);

        public CommandResult Apply(PlanningCommand command, double nowSeconds)
        {
            Guard.NotNull(command, nameof(command));
            if (!IsOpen(nowSeconds)) return CommandResult.Reject(CommandRejection.NotInPlanningPhase);

            if (command is SetReadyCommand ready) return SetReady(ready.IsReady, nowSeconds);

            // Ready freezes the plan: cancel it first to edit again (GDD §3, TBD).
            if (IsReady) return CommandResult.Reject(CommandRejection.AlreadyReady);

            switch (command)
            {
                case SelectWeaponCommand select: return SelectWeapon(select.Weapon, nowSeconds);
                case SetBodyMoveCommand body: return SetBodyMove(body.Move);
                case SetPathCommand path: return SetPath(path.Path, nowSeconds);
                case ClearPathCommand _: return ClearPath(nowSeconds);

                case UseSignatureCommand signature: return UseSignature(signature.Signature, nowSeconds);
                default:
                    throw new ArgumentException($"Unknown planning command {command.GetType().Name}.", nameof(command));
            }
        }

        private CommandResult SelectWeapon(WeaponId weapon, double nowSeconds)
        {
            if (!Constraints.IsWeaponAvailable(weapon, _loadout)) return CommandResult.Reject(CommandRejection.WeaponNotInLoadout);

            // Picking the weapon already held is not a switch: nothing is erased and no lock-out applies.
            if (weapon == _weapon) return CommandResult.Ok;

            if (IsWeaponSwitchLockedOut(nowSeconds)) return CommandResult.Reject(CommandRejection.WeaponSwitchLockedOut);

            _weapon = weapon;
            ErasePath(); // GDD §6 (Decided): switching erases the path already drawn.
            _lastWeaponSwitchSeconds = nowSeconds;
            return CommandResult.Ok;
        }

        private CommandResult SetBodyMove(BodyMove move)
        {
            // A move that defines its own body move leaves no swipe to set (§8): cancel the move to swipe again.
            if (_signatureMove != null && _signatureMove.OverridesBodyMove) return CommandResult.Reject(CommandRejection.BodyMoveNotAllowed);
            if (!Constraints.IsBodyMoveAllowed(move)) return CommandResult.Reject(CommandRejection.BodyMoveNotAllowed);

            _bodyMove = move;
            return CommandResult.Ok;
        }

        private CommandResult SetPath(WeaponPath stroke, double nowSeconds)
        {
            if (IsDrawingLockedOut(nowSeconds)) return CommandResult.Reject(CommandRejection.DrawingLockedOut);
            if (stroke == null || stroke.Points.Count == 0) return CommandResult.Reject(CommandRejection.InvalidPath);

            WeaponStats stats = FindWeaponStats().WithInkLengthMultiplier(Constraints.InkLengthMultiplier);
            if (stats.InkLengthUnits <= 0f) return CommandResult.Reject(CommandRejection.InkBudgetExceeded);

            // A redraw replaces a signature move's pattern: it never builds on it.
            WeaponPath basis = _signature.IsEmpty ? _drawn : WeaponPath.Empty;
            BuiltPath built = _pathBuilder.Build(basis, stroke, _weaponTips.TipLocal(_weapon), stats, Settings.Paths);

            // A stroke that adds no movement (for example a single point on the weapon's tip) is not a path.
            if (built.Path.IsEmpty) return CommandResult.Reject(CommandRejection.InvalidPath);

            ClearSignature();
            _drawn = built.Drawn;
            _builtPath = built;
            _lastPathChangeSeconds = nowSeconds;
            return CommandResult.Ok;
        }

        /// <summary>
        /// Tapping a signature move (§8): its pattern becomes the path. Every refusal changes nothing. Choosing a move again sets it
        /// again (the same pattern); cancelling it is clearing the path or drawing.
        /// </summary>
        private CommandResult UseSignature(SignatureMoveId id, double nowSeconds)
        {
            if (!Constraints.CanUseSignature) return CommandResult.Reject(CommandRejection.SignatureUnavailable);

            SignatureMoveStats move = SignatureCatalogue.Find(_signatures, id);
            if (move == null) return CommandResult.Reject(CommandRejection.SignatureUnavailable);
            if (IsDrawingLockedOut(nowSeconds)) return CommandResult.Reject(CommandRejection.DrawingLockedOut);
            if (!SignatureCharge.IsAvailable) return CommandResult.Reject(CommandRejection.SignatureNotCharged);
            if (!move.AllowsWeapon(_weapon)) return CommandResult.Reject(CommandRejection.SignatureWrongWeapon);
            if (move.OverridesBodyMove && !Constraints.IsBodyMoveAllowed(move.BodyMoveOverride))
                return CommandResult.Reject(CommandRejection.BodyMoveNotAllowed);

            WeaponStats weapon = FindWeaponStats().WithInkLengthMultiplier(Constraints.InkLengthMultiplier);
            if (move.ConsumesInk && weapon.InkLengthUnits <= 0f) return CommandResult.Reject(CommandRejection.InkBudgetExceeded);

            BuiltPath built = _signaturePathBuilder.Build(move, _weaponTips.TipLocal(_weapon), weapon, Settings.Paths);
            if (built.Path.IsEmpty) return CommandResult.Reject(CommandRejection.InvalidPath);

            _drawn = built.Drawn;
            _builtPath = built;
            _signature = move.MoveId;
            _signatureMove = move;
            _lastPathChangeSeconds = nowSeconds;
            return CommandResult.Ok;
        }

        private CommandResult ClearPath(double nowSeconds)
        {
            if (IsDrawingLockedOut(nowSeconds)) return CommandResult.Reject(CommandRejection.DrawingLockedOut);

            ErasePath();
            _lastPathChangeSeconds = nowSeconds;
            return CommandResult.Ok;
        }

        private CommandResult SetReady(bool ready, double nowSeconds)
        {
            if (ready)
            {
                if (!IsReady)
                {
                    IsReady = true;
                    _readySeconds = nowSeconds;
                }

                return CommandResult.Ok;
            }

            if (!IsReady) return CommandResult.Ok;

            // D9: cancel is allowed until the lock-out starts (both halves are flags, not decided).
            bool cancelAllowed = Settings.Match.AllowReadyCancel &&
                                 (Settings.Match.AllowReadyCancelDuringLockout || !IsInLockoutWindow(nowSeconds));
            if (!cancelAllowed) return CommandResult.Reject(CommandRejection.ReadyCancelNotAllowed);

            IsReady = false;
            _readySeconds = null;
            return CommandResult.Ok;
        }

        /// <summary>No path any more, and so no signature move: its pattern was the path (§6 switching, §8 cancelling).</summary>
        private void ErasePath()
        {
            _drawn = WeaponPath.Empty;
            _builtPath = null;
            ClearSignature();
        }

        private void ClearSignature()
        {
            _signature = default;
            _signatureMove = null;
        }

        private WeaponStats FindWeaponStats() =>
            Settings.FindWeapon(_weapon)
            ?? throw new InvalidOperationException($"The rules settings have no stats for weapon '{_weapon}'.");
    }
}
