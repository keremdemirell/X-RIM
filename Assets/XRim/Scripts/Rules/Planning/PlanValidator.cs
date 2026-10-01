using System.Collections.Generic;
using XRim.Core;
using XRim.Rules.Paths;
using XRim.Rules.Settings;

namespace XRim.Rules.Planning
{
    /// <summary>
    /// Authority-side check of a locked plan against the authority's own clock and rules (GDD §18: "the server should
    /// check every input (ink budget, weapon lock-out timing, Ready time)"). <see cref="PlanningSession"/> already
    /// enforces all of this as commands arrive; this is the second line of defence for plans that did not come
    /// through a session, such as a plan sent whole by a remote client.
    /// <list type="bullet">
    /// <item>The weapon is in the loadout and has stats.</item>
    /// <item>Timing, from the <see cref="PlanningAudit"/>: nothing happened after the deadline, the last weapon switch
    /// was outside the lock-out, and drawing was outside it when drawing is locked out.</item>
    /// <item>The body move is allowed by the constraints, and no signature move is used (Session 15).</item>
    /// <item>The path costs no more than the weapon's ink (scaled by the constraints), has no too-sharp turn, and stays
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

        private readonly RulesSettings _settings;
        private readonly RulePolicies _policies;

        public PlanValidator(RulesSettings settings, RulePolicies policies)
        {
            _settings = Guard.NotNull(settings, nameof(settings));
            _policies = Guard.NotNull(policies, nameof(policies));
        }

        /// <summary>Validates what a session holds: its plan, its audit, its loadout and its constraints.</summary>
        public CommandResult Validate(PlanningSession session)
        {
            Guard.NotNull(session, nameof(session));
            return Validate(session.CurrentPlan, session.Audit, session.Loadout, session.Constraints);
        }

        public CommandResult Validate(TurnPlan plan, PlanningAudit audit, IReadOnlyList<WeaponId> loadout,
            PlanningConstraints constraints)
        {
            Guard.NotNull(plan, nameof(plan));
            Guard.NotNull(loadout, nameof(loadout));
            Guard.NotNull(constraints, nameof(constraints));

            WeaponStats weapon = _settings.FindWeapon(plan.Weapon);
            if (weapon == null || !InLoadout(loadout, plan.Weapon))
            {
                return CommandResult.Reject(CommandRejection.WeaponNotInLoadout);
            }

            CommandResult timing = CheckTiming(audit);
            if (!timing.Accepted) return timing;

            if (!constraints.IsBodyMoveAllowed(plan.BodyMove))
            {
                return CommandResult.Reject(CommandRejection.BodyMoveNotAllowed);
            }

            // Signature moves arrive in Session 15 (GDD §8); until then no plan may carry one.
            if (!plan.Signature.IsEmpty) return CommandResult.Reject(CommandRejection.SignatureUnavailable);

            return CheckPath(plan.Path, weapon.WithInkLengthMultiplier(constraints.InkLengthMultiplier));
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

        private static bool InLoadout(IReadOnlyList<WeaponId> loadout, WeaponId weapon)
        {
            for (int i = 0; i < loadout.Count; i++)
            {
                if (loadout[i] == weapon) return true;
            }

            return false;
        }
    }
}
