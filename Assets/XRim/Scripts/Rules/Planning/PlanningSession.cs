using System;
using System.Collections.Generic;
using XRim.Core;
using XRim.Rules.Paths;
using XRim.Rules.Settings;

namespace XRim.Rules.Planning
{
    /// <summary>
    /// One player's planning phase. Clients run it for instant feedback (ink meter, lock-out);
    /// the authority runs the same code to validate (GDD §18).
    /// <list type="bullet">
    /// <item>Weapon switch: only from the loadout, erases the drawn path, refused in the final
    /// <see cref="MatchSettings.WeaponSwitchLockoutSeconds"/> and public at once (§6).</item>
    /// <item>Path: built through <see cref="PathBuilder"/> (start, reach and ink rules, §6), so a stroke past the
    /// ink budget is cut where the ink runs out, never refused.</item>
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

        private WeaponId _weapon;
        private BodyMove _bodyMove = BodyMove.None;
        private WeaponPath _drawn = WeaponPath.Empty;
        private BuiltPath _builtPath;
        private SignatureMoveId _signature;
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

        /// <summary>What <see cref="PathBuilder"/> made of the last accepted stroke, for the preview. Null when no path is set.</summary>
        public BuiltPath CurrentPath => _builtPath;

        /// <summary>Whether the opponent may see that a signature move is charged (GDD §8, TBD).</summary>
        public bool OpponentSeesChargedSignature => Settings.Signature.OpponentSeesCharged;

        private RulesSettings Settings { get; }

        public PlanningSession(Side side, WeaponId currentWeapon, IReadOnlyList<WeaponId> loadout,
            IWeaponTipSource weaponTips, PlanningConstraints constraints, double deadlineSeconds,
            RulesSettings settings, RulePolicies policies)
        {
            Side = side;
            _weapon = currentWeapon;
            _loadout = Guard.NotNull(loadout, nameof(loadout));
            _weaponTips = Guard.NotNull(weaponTips, nameof(weaponTips));
            Constraints = Guard.NotNull(constraints, nameof(constraints));
            DeadlineSeconds = deadlineSeconds;
            Settings = Guard.NotNull(settings, nameof(settings));
            _policies = Guard.NotNull(policies, nameof(policies));
            _pathBuilder = new PathBuilder(_policies);
        }

        /// <summary>Whatever is set right now. This is what executes when the timer ends, Ready or not (§3, Decided).</summary>
        public TurnPlan CurrentPlan =>
            new TurnPlan(_weapon, _bodyMove, _builtPath != null ? _builtPath.Path : WeaponPath.Empty, _signature, IsReady);

        public PublicPlanningState PublicState
        {
            get
            {
                bool charged = _policies.SignatureCharge != null && _policies.SignatureCharge.IsAvailable(Side);
                return _policies.PublicState.Build(this, charged);
            }
        }

        public PlanningAudit Audit =>
            new PlanningAudit(DeadlineSeconds, _lastWeaponSwitchSeconds, _lastPathChangeSeconds, _readySeconds);

        /// <summary>True until the deadline: from the deadline on, commands are refused.</summary>
        public bool IsOpen(double nowSeconds) => nowSeconds < DeadlineSeconds;

        /// <summary>
        /// True in the final <see cref="MatchSettings.WeaponSwitchLockoutSeconds"/> of planning, boundary included:
        /// at exactly 1.5 s left a switch is already refused (§6).
        /// </summary>
        public bool IsInLockoutWindow(double nowSeconds) =>
            nowSeconds >= DeadlineSeconds - Settings.Match.WeaponSwitchLockoutSeconds;

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

                // Signature moves arrive in Session 15 (GDD §8, all TBD). Until a charge model and a move catalogue
                // exist, every request is refused through the existing rejection.
                case UseSignatureCommand _: return CommandResult.Reject(CommandRejection.SignatureUnavailable);
                default:
                    throw new ArgumentException($"Unknown planning command {command.GetType().Name}.", nameof(command));
            }
        }

        private CommandResult SelectWeapon(WeaponId weapon, double nowSeconds)
        {
            if (!InLoadout(weapon)) return CommandResult.Reject(CommandRejection.WeaponNotInLoadout);

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

            BuiltPath built = _pathBuilder.Build(_drawn, stroke, _weaponTips.TipLocal(_weapon), stats, Settings.Paths);

            // A stroke that adds no movement (for example a single point on the weapon's tip) is not a path.
            if (built.Path.IsEmpty) return CommandResult.Reject(CommandRejection.InvalidPath);

            _drawn = built.Drawn;
            _builtPath = built;
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

        private void ErasePath()
        {
            _drawn = WeaponPath.Empty;
            _builtPath = null;
        }

        private bool InLoadout(WeaponId weapon)
        {
            for (int i = 0; i < _loadout.Count; i++)
            {
                if (_loadout[i] == weapon) return true;
            }

            return false;
        }

        private WeaponStats FindWeaponStats() =>
            Settings.FindWeapon(_weapon)
            ?? throw new InvalidOperationException($"The rules settings have no stats for weapon '{_weapon}'.");
    }
}
