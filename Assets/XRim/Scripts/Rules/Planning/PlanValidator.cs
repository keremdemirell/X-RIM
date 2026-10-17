using System.Collections.Generic;
using XRim.Core;
using XRim.Rules.Paths;
using XRim.Rules.Settings;
using XRim.Rules.Signature;

namespace XRim.Rules.Planning
{
    /// <summary>
    /// Authority-side check of a locked plan against the authority's own clock and rules (GDD §18: "the server should
    /// check every input (ink budget, weapon lock-out timing, Ready time)"). <see cref="PlanningSession"/> already
    /// enforces all of this as commands arrive; this is the second line of defence for plans that did not come
    /// through a session, such as a plan sent whole by a remote client.
    /// <list type="bullet">
    /// <item>The weapon has stats and the constraints allow it this turn (the loadout or a §12 extra option).</item>
    /// <item>Timing, from the <see cref="PlanningAudit"/>: nothing happened after the deadline, the last weapon switch
    /// was outside the lock-out, and drawing was outside it when drawing is locked out.</item>
    /// <item>The body move is allowed by the constraints.</item>
    /// <item>A signature move (§8): signature moves are allowed this turn, the dummy brought the move and is charged, the weapon is one
    /// the move works with, a move that defines its own body move runs with it, and the path is exactly the pattern
    /// <see cref="SignaturePathBuilder"/> makes of it. Without a <see cref="SignaturePlanContext"/> every signature plan is refused.</item>
    /// <item>Otherwise the path costs no more than the weapon's ink (scaled by the constraints), has no too-sharp turn, and stays
    /// inside the reach limit. The path must be the one that executes, as <see cref="PathBuilder"/> makes it;
    /// a remote authority rebuilds it from the raw stroke first.</item>
    /// </list>
    /// A plan with nothing set is valid: an idle turn is legal (GDD §3).
    /// </summary>
    public sealed class PlanValidator
    {
        /// <summary>Absorbs float error when a cut path lands exactly on its ink budget.</summary>
        private const float InkToleranceUnits = 0.01f;

        /// <summary>Absorbs float error for points that sit exactly on the reach circle.</summary>
        private const float ReachToleranceUnits = 0.1f;

        /// <summary>Two path points this close are the same point (the pattern is rebuilt in floats, never typed in).</summary>
        private const float SamePointToleranceUnits = 0.01f;

        private readonly RulesSettings _settings;
        private readonly RulePolicies _policies;
        private readonly SignaturePathBuilder _signaturePaths;

        public PlanValidator(RulesSettings settings, RulePolicies policies)
        {
            _settings = Guard.NotNull(settings, nameof(settings));
            _policies = Guard.NotNull(policies, nameof(policies));
            _signaturePaths = new SignaturePathBuilder(policies);
        }

        /// <summary>Validates what a session holds: its plan, its audit, its loadout, its constraints and its signature moves.</summary>
        public CommandResult Validate(PlanningSession session)
        {
            Guard.NotNull(session, nameof(session));
            var signatures = new SignaturePlanContext(session.SignatureCharge, session.Signatures, session.WeaponTips);
            return Validate(session.CurrentPlan, session.Audit, session.Loadout, session.Constraints, signatures);
        }

        /// <param name="signatures">What judging a signature move needs (§8); null refuses every plan that holds one.</param>
        public CommandResult Validate(TurnPlan plan, PlanningAudit audit, IReadOnlyList<WeaponId> loadout,
            PlanningConstraints constraints, SignaturePlanContext signatures = null)
        {
            Guard.NotNull(plan, nameof(plan));
            Guard.NotNull(loadout, nameof(loadout));
            Guard.NotNull(constraints, nameof(constraints));

            WeaponStats weapon = _settings.FindWeapon(plan.Weapon);
            if (weapon == null || !constraints.IsWeaponAvailable(plan.Weapon, loadout))
            {
                return CommandResult.Reject(CommandRejection.WeaponNotInLoadout);
            }

            CommandResult timing = CheckTiming(audit);
            if (!timing.Accepted) return timing;

            if (!constraints.IsBodyMoveAllowed(plan.BodyMove))
            {
                return CommandResult.Reject(CommandRejection.BodyMoveNotAllowed);
            }

            WeaponStats weaponForTurn = weapon.WithInkLengthMultiplier(constraints.InkLengthMultiplier);
            if (!plan.Signature.IsEmpty) return CheckSignature(plan, weaponForTurn, constraints, signatures);

            return CheckPath(plan.Path, weaponForTurn);
        }

        /// <summary>
        /// A plan that holds a signature move (§8): the move is real and brought, signature moves are allowed and the dummy is charged,
        /// the weapon is tied to it or free, the body move is the move's own when it defines one, and the path is the pattern.
        /// </summary>
        private CommandResult CheckSignature(TurnPlan plan, WeaponStats weapon, PlanningConstraints constraints, SignaturePlanContext signatures)
        {
            if (signatures == null || !constraints.CanUseSignature) return CommandResult.Reject(CommandRejection.SignatureUnavailable);

            SignatureMoveStats move = SignatureCatalogue.Find(signatures.Moves, plan.Signature);
            if (move == null) return CommandResult.Reject(CommandRejection.SignatureUnavailable);
            if (!signatures.Charge.IsAvailable) return CommandResult.Reject(CommandRejection.SignatureNotCharged);
            if (!move.AllowsWeapon(plan.Weapon)) return CommandResult.Reject(CommandRejection.SignatureWrongWeapon);
            if (move.OverridesBodyMove && plan.BodyMove != move.BodyMoveOverride)
                return CommandResult.Reject(CommandRejection.BodyMoveNotAllowed);

            BuiltPath expected = _signaturePaths.Build(move, signatures.WeaponTips.TipLocal(plan.Weapon), weapon, _settings.Paths);
            return SamePath(expected.Path, plan.Path)
                ? CommandResult.Ok
                : CommandResult.Reject(CommandRejection.InvalidPath);
        }

        private static bool SamePath(WeaponPath a, WeaponPath b)
        {
            if (a.Points.Count != b.Points.Count) return false;
            for (int i = 0; i < a.Points.Count; i++)
            {
                if (Vec2.Distance(a.Points[i], b.Points[i]) > SamePointToleranceUnits) return false;
            }

            return true;
        }

        private CommandResult CheckTiming(PlanningAudit audit)
        {
            double deadline = audit.DeadlineSeconds;
            float lockout = _settings.Match.WeaponSwitchLockoutSeconds;

            if (IsLate(audit.LastWeaponSwitchSeconds, deadline) || IsLate(audit.LastPathChangeSeconds, deadline) ||
                IsLate(audit.ReadySeconds, deadline))
            {
                return CommandResult.Reject(CommandRejection.NotInPlanningPhase);
            }

            if (audit.LastWeaponSwitchSeconds is double switchedAt &&
                PlanningTiming.IsInLockoutWindow(switchedAt, deadline, lockout))
            {
                return CommandResult.Reject(CommandRejection.WeaponSwitchLockedOut);
            }

            if (!_settings.Match.AllowDrawingDuringLockout && audit.LastPathChangeSeconds is double drawnAt &&
                PlanningTiming.IsInLockoutWindow(drawnAt, deadline, lockout))
            {
                return CommandResult.Reject(CommandRejection.DrawingLockedOut);
            }

            return CommandResult.Ok;
        }

        private CommandResult CheckPath(WeaponPath path, WeaponStats weapon)
        {
            if (path.IsEmpty) return CommandResult.Ok;

            InkMeasurement ink = _policies.InkCost.Measure(path, weapon);
            if (ink.CostUnits > weapon.InkLengthUnits + InkToleranceUnits)
            {
                return CommandResult.Reject(CommandRejection.InkBudgetExceeded);
            }

            // A too-sharp turn must already have been cut off the path; a surviving one is not a legal trajectory.
            if (!ink.IsValid) return CommandResult.Reject(CommandRejection.InvalidPath);

            ReachLimit reach = ReachLimit.For(_settings.Paths, weapon);
            for (int i = 0; i < path.Points.Count; i++)
            {
                if (!reach.Contains(path.Points[i], ReachToleranceUnits))
                {
                    return CommandResult.Reject(CommandRejection.InvalidPath);
                }
            }

            return CommandResult.Ok;
        }

        private static bool IsLate(double? happenedAtSeconds, double deadlineSeconds) =>
            happenedAtSeconds.HasValue && !PlanningTiming.IsOpen(happenedAtSeconds.Value, deadlineSeconds);
    }
}
