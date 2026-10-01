using System.Collections.Generic;
using NUnit.Framework;
using XRim.Core;
using XRim.Rules;
using XRim.Rules.Paths;
using XRim.Rules.Planning;
using XRim.Rules.Settings;
using static XRim.Tests.EditMode.Rules.Planning.PlanningTestData;

namespace XRim.Tests.EditMode.Rules.Planning
{
    /// <summary>The authority re-checks a locked plan on its own clock (GDD §18).</summary>
    public sealed class PlanValidatorTests
    {
        private const double JustBefore = 0.001;

        private RulesSettings _settings;
        private PlanningConstraints _constraints;
        private PlanValidator _validator;

        [SetUp]
        public void SetUp()
        {
            _settings = GddStartingValues.CreateRulesSettings();
            _constraints = PlanningConstraints.CreateDefault(_settings.Match);
            _validator = new PlanValidator(_settings, new RulePolicies());
        }

        private static PlanningAudit Audit(double? switchedAt = null, double? drawnAt = null, double? readyAt = null) =>
            new PlanningAudit(DeadlineSeconds, switchedAt, drawnAt, readyAt);

        private static TurnPlan Plan(WeaponId weapon, WeaponPath path = null, BodyMove move = BodyMove.None,
            SignatureMoveId signature = default) =>
            new TurnPlan(weapon, move, path ?? WeaponPath.Empty, signature, false);

        /// <summary>A straight line along y = 100 with a point every 10 units, as the resampler makes them.</summary>
        private static WeaponPath Line(float fromX, float toX)
        {
            var points = new List<Vec2>();
            float step = fromX <= toX ? 10f : -10f;
            for (float x = fromX; step > 0f ? x <= toX : x >= toX; x += step)
            {
                points.Add(new Vec2(x, 100f));
            }

            return new WeaponPath(points);
        }

        private CommandResult Check(TurnPlan plan, PlanningAudit? audit = null, IReadOnlyList<WeaponId> loadout = null) =>
            _validator.Validate(plan, audit ?? Audit(), loadout ?? Loadout, _constraints);

        // --- A plan that is fine ---------------------------------------------------------------

        [Test]
        public void AnIdlePlan_IsValid()
        {
            Assert.That(Check(Plan(WeaponIds.Rapier)).Accepted, Is.True, "an idle turn is legal (GDD §3)");
        }

        [Test]
        public void APlanBuiltByASession_IsValid()
        {
            PlanningSession session = CreateSession(_settings);
            session.Apply(new SelectWeaponCommand(WeaponIds.Mace), 1.0);
            session.Apply(new SetBodyMoveCommand(BodyMove.Lunge), 2.0);
            session.Apply(new SetPathCommand(ThereAndBackAgain()), 3.0); // over budget: the session cuts it
            session.Apply(new SetReadyCommand(true), 4.0);

            Assert.That(_validator.Validate(session).Accepted, Is.True);
        }

        [Test]
        public void APathThatUsesTheWholeBudget_IsValid()
        {
            WeaponPath path = Line(40f, 640f); // 600 units, the rapier's whole budget, ending on the reach circle

            Assert.That(Check(Plan(WeaponIds.Rapier, path)).Accepted, Is.True);
        }

        // --- Loadout ---------------------------------------------------------------------------

        [Test]
        public void AWeaponOutsideTheLoadout_IsRejected()
        {
            Assert.That(Check(Plan(WeaponIds.Spear)).Rejection, Is.EqualTo(CommandRejection.WeaponNotInLoadout));
        }

        [Test]
        public void AWeaponWithoutStats_IsRejected()
        {
            var bogus = new WeaponId("bogus");

            CommandResult result = Check(Plan(bogus), loadout: new[] { bogus });

            Assert.That(result.Rejection, Is.EqualTo(CommandRejection.WeaponNotInLoadout));
        }

        // --- Ink (§6) --------------------------------------------------------------------------

        [Test]
        public void AnOverBudgetPlan_IsRejected()
        {
            CommandResult result = Check(Plan(WeaponIds.Rapier, ThereAndBackAgain()));

            Assert.That(result.Rejection, Is.EqualTo(CommandRejection.InkBudgetExceeded));
        }

        [Test]
        public void TheBudgetFollowsTheWeapon_NotThePlayer()
        {
            WeaponPath path = Line(100f, 400f); // 300 units: fine for the rapier, too long for the mace's 200

            Assert.That(Check(Plan(WeaponIds.Rapier, path)).Accepted, Is.True);
            Assert.That(Check(Plan(WeaponIds.Mace, path)).Rejection, Is.EqualTo(CommandRejection.InkBudgetExceeded));
        }

        [Test]
        public void TheBudgetIsScaledByTheConstraints()
        {
            _constraints.InkLengthMultiplier = 0.5f; // 300 of the rapier's 600

            Assert.That(Check(Plan(WeaponIds.Rapier, Line(100f, 350f))).Accepted, Is.True);
            Assert.That(Check(Plan(WeaponIds.Rapier, Line(100f, 500f))).Rejection, Is.EqualTo(CommandRejection.InkBudgetExceeded));
        }

        [Test]
        public void ATooSharpTurn_IsRejected_WhenRigidityIsOn()
        {
            _settings.FindWeapon(WeaponIds.Spear).Rigidity.Enabled = true;
            var points = new List<Vec2>(Line(100f, 300f).Points);
            points.AddRange(Line(290f, 100f).Points); // straight back on itself: a 180 degree turn
            var plan = Plan(WeaponIds.Spear, new WeaponPath(points));

            CommandResult result = Check(plan, loadout: new[] { WeaponIds.Spear });

            Assert.That(result.Rejection, Is.EqualTo(CommandRejection.InvalidPath));
        }

        [Test]
        public void APathBeyondReach_IsRejected()
        {
            WeaponPath path = Line(500f, 700f); // rapier reach is 640 from the shoulder

            Assert.That(Check(Plan(WeaponIds.Rapier, path)).Rejection, Is.EqualTo(CommandRejection.InvalidPath));
        }

        // --- Body moves and signature moves ----------------------------------------------------

        [Test]
        public void AForbiddenBodyMove_IsRejected()
        {
            _constraints.ForbidBodyMove(BodyMove.Jump);

            Assert.That(Check(Plan(WeaponIds.Rapier, move: BodyMove.Jump)).Rejection, Is.EqualTo(CommandRejection.BodyMoveNotAllowed));
            Assert.That(Check(Plan(WeaponIds.Rapier, move: BodyMove.Crouch)).Accepted, Is.True);
        }

        [Test]
        public void ASignatureMove_IsRejectedUntilSession15()
        {
            CommandResult result = Check(Plan(WeaponIds.Rapier, signature: new SignatureMoveId("head_impact")));

            Assert.That(result.Rejection, Is.EqualTo(CommandRejection.SignatureUnavailable));
        }

        // --- Timing, on the authority's clock (§18) --------------------------------------------

        [Test]
        public void ALateWeaponSwitch_IsRejected()
        {
            TurnPlan plan = Plan(WeaponIds.Mace);

            Assert.That(Check(plan, Audit(switchedAt: LockoutStartSeconds - JustBefore)).Accepted, Is.True);
            Assert.That(Check(plan, Audit(switchedAt: LockoutStartSeconds)).Rejection, Is.EqualTo(CommandRejection.WeaponSwitchLockedOut));
            Assert.That(Check(plan, Audit(switchedAt: DeadlineSeconds - JustBefore)).Rejection,
                Is.EqualTo(CommandRejection.WeaponSwitchLockedOut));
        }

        [Test]
        public void TheLockoutLengthIsTunable()
        {
            _settings.Match.WeaponSwitchLockoutSeconds = 3f;
            TurnPlan plan = Plan(WeaponIds.Mace);

            Assert.That(Check(plan, Audit(switchedAt: 9.0 - JustBefore)).Accepted, Is.True);
            Assert.That(Check(plan, Audit(switchedAt: 9.0)).Rejection, Is.EqualTo(CommandRejection.WeaponSwitchLockedOut));
        }

        [Test]
        public void ADrawingInTheLockout_IsRejectedOnlyWhenTheFlagLocksDrawing()
        {
            TurnPlan plan = Plan(WeaponIds.Rapier, Line(100f, 300f));
            PlanningAudit drawnInLockout = Audit(drawnAt: LockoutStartSeconds + 0.5);

            Assert.That(Check(plan, drawnInLockout).Accepted, Is.True, "D6 default: drawing is allowed");

            _settings.Match.AllowDrawingDuringLockout = false;
            Assert.That(Check(plan, drawnInLockout).Rejection, Is.EqualTo(CommandRejection.DrawingLockedOut));
            Assert.That(Check(plan, Audit(drawnAt: LockoutStartSeconds - JustBefore)).Accepted, Is.True);
        }

        [Test]
        public void AnythingAtOrAfterTheDeadline_IsRejected()
        {
            TurnPlan plan = Plan(WeaponIds.Rapier);

            Assert.That(Check(plan, Audit(switchedAt: DeadlineSeconds)).Rejection, Is.EqualTo(CommandRejection.NotInPlanningPhase));
            Assert.That(Check(plan, Audit(drawnAt: DeadlineSeconds + 1.0)).Rejection, Is.EqualTo(CommandRejection.NotInPlanningPhase));
            Assert.That(Check(plan, Audit(readyAt: DeadlineSeconds)).Rejection, Is.EqualTo(CommandRejection.NotInPlanningPhase));
        }

        [Test]
        public void ReadyInTheLockout_IsFine_ButNotAfterTheDeadline()
        {
            TurnPlan plan = Plan(WeaponIds.Rapier);

            Assert.That(Check(plan, Audit(readyAt: DeadlineSeconds - JustBefore)).Accepted, Is.True);
            Assert.That(Check(plan, Audit(readyAt: DeadlineSeconds)).Accepted, Is.False);
        }

        [Test]
        public void TheValidatorAndTheSession_AgreeOnTheLockoutBoundary()
        {
            foreach (double at in new[] { 1.0, LockoutStartSeconds - JustBefore, LockoutStartSeconds, LockoutStartSeconds + JustBefore, 11.9 })
            {
                PlanningSession session = CreateSession(_settings);
                bool sessionAccepted = session.Apply(new SelectWeaponCommand(WeaponIds.Mace), at).Accepted;

                CommandResult validated = Check(Plan(WeaponIds.Mace), Audit(switchedAt: at));

                Assert.That(validated.Accepted, Is.EqualTo(sessionAccepted), $"at {at} s");
            }
        }

        [Test]
        public void PlanningTiming_HasTheBoundariesTheRulesState()
        {
            Assert.That(PlanningTiming.IsOpen(DeadlineSeconds - JustBefore, DeadlineSeconds), Is.True);
            Assert.That(PlanningTiming.IsOpen(DeadlineSeconds, DeadlineSeconds), Is.False);
            Assert.That(PlanningTiming.IsInLockoutWindow(LockoutStartSeconds - JustBefore, DeadlineSeconds, 1.5f), Is.False);
            Assert.That(PlanningTiming.IsInLockoutWindow(LockoutStartSeconds, DeadlineSeconds, 1.5f), Is.True);
        }
    }
}
