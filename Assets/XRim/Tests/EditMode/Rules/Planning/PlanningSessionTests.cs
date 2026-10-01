using NUnit.Framework;
using XRim.Core;
using XRim.Rules;
using XRim.Rules.Paths;
using XRim.Rules.Planning;
using XRim.Rules.Settings;
using static XRim.Tests.EditMode.Rules.Planning.PlanningTestData;

namespace XRim.Tests.EditMode.Rules.Planning
{
    /// <summary>Every command rule of one planning phase (GDD §3, §6).</summary>
    public sealed class PlanningSessionTests
    {
        private const float PathTolerance = 0.5f;
        private const double JustBefore = 0.001;

        // --- Weapon switching (§6) -------------------------------------------------------------

        [Test]
        public void SelectWeapon_FromTheLoadout_IsAccepted_AndPublicAtOnce()
        {
            PlanningSession session = CreateSession();

            CommandResult result = session.Apply(new SelectWeaponCommand(WeaponIds.Mace), 1.0);

            Assert.That(result.Accepted, Is.True);
            Assert.That(session.CurrentPlan.Weapon, Is.EqualTo(WeaponIds.Mace));
            Assert.That(session.PublicState.Weapon, Is.EqualTo(WeaponIds.Mace), "§6: the switch is shown to the opponent immediately");
        }

        [Test]
        public void SelectWeapon_OutsideTheLoadout_IsRefused()
        {
            PlanningSession session = CreateSession();

            CommandResult result = session.Apply(new SelectWeaponCommand(WeaponIds.Spear), 1.0);

            Assert.That(result.Rejection, Is.EqualTo(CommandRejection.WeaponNotInLoadout));
            Assert.That(session.Weapon, Is.EqualTo(WeaponIds.Rapier));
        }

        [Test]
        public void SelectWeapon_ErasesTheDrawnPath()
        {
            PlanningSession session = CreateSession();
            session.Apply(new SetPathCommand(Thrust(400f)), 1.0);
            Assert.That(session.CurrentPlan.Path.IsEmpty, Is.False, "precondition: a path is drawn");

            session.Apply(new SelectWeaponCommand(WeaponIds.Mace), 2.0);

            Assert.That(session.CurrentPlan.Path.IsEmpty, Is.True, "§6 (Decided): switching erases the path");
            Assert.That(session.CurrentPath, Is.Null);
        }

        [Test]
        public void SelectWeapon_TheSameWeapon_IsNotASwitch()
        {
            PlanningSession session = CreateSession();
            session.Apply(new SetPathCommand(Thrust(400f)), 1.0);

            CommandResult result = session.Apply(new SelectWeaponCommand(WeaponIds.Rapier), LockoutStartSeconds + 0.5);

            Assert.That(result.Accepted, Is.True, "no lock-out applies to a switch that changes nothing");
            Assert.That(session.CurrentPlan.Path.IsEmpty, Is.False, "the path stays");
        }

        [Test]
        public void SelectWeapon_JustBeforeTheLockout_IsAccepted()
        {
            PlanningSession session = CreateSession();

            CommandResult result = session.Apply(new SelectWeaponCommand(WeaponIds.Mace), LockoutStartSeconds - JustBefore);

            Assert.That(result.Accepted, Is.True);
        }

        [Test]
        public void SelectWeapon_ExactlyWhenTheLockoutStarts_IsRefused()
        {
            PlanningSession session = CreateSession();
            session.Apply(new SetPathCommand(Thrust(400f)), 1.0);

            CommandResult result = session.Apply(new SelectWeaponCommand(WeaponIds.Mace), LockoutStartSeconds);

            Assert.That(result.Rejection, Is.EqualTo(CommandRejection.WeaponSwitchLockedOut), "1.5 s left: already locked out");
            Assert.That(session.Weapon, Is.EqualTo(WeaponIds.Rapier));
            Assert.That(session.CurrentPlan.Path.IsEmpty, Is.False, "a refused switch erases nothing");
        }

        [Test]
        public void SelectWeapon_InsideTheLockout_IsRefused()
        {
            PlanningSession session = CreateSession();

            CommandResult result = session.Apply(new SelectWeaponCommand(WeaponIds.Mace), DeadlineSeconds - JustBefore);

            Assert.That(result.Rejection, Is.EqualTo(CommandRejection.WeaponSwitchLockedOut));
        }

        [Test]
        public void SelectWeapon_LockoutLengthIsTunable()
        {
            RulesSettings settings = GddStartingValues.CreateRulesSettings();
            settings.Match.WeaponSwitchLockoutSeconds = 3f;
            PlanningSession session = CreateSession(settings);

            Assert.That(session.Apply(new SelectWeaponCommand(WeaponIds.Mace), 9.0 - JustBefore).Accepted, Is.True);
            Assert.That(session.Apply(new SelectWeaponCommand(WeaponIds.Shield), 9.0).Rejection,
                Is.EqualTo(CommandRejection.WeaponSwitchLockedOut));
        }

        [Test]
        public void SelectWeapon_RespectsTheConstraints()
        {
            RulesSettings settings = GddStartingValues.CreateRulesSettings();
            var constraints = PlanningConstraints.CreateDefault(settings.Match);
            constraints.CanSwitchWeapon = false;
            PlanningSession session = CreateSession(settings, constraints);

            CommandResult result = session.Apply(new SelectWeaponCommand(WeaponIds.Mace), 1.0);

            Assert.That(result.Rejection, Is.EqualTo(CommandRejection.WeaponSwitchLockedOut));
        }

        // --- Body moves ------------------------------------------------------------------------

        [Test]
        public void SetBodyMove_IsRecorded_AndNoneClearsIt()
        {
            PlanningSession session = CreateSession();

            Assert.That(session.Apply(new SetBodyMoveCommand(BodyMove.Lunge), 1.0).Accepted, Is.True);
            Assert.That(session.CurrentPlan.BodyMove, Is.EqualTo(BodyMove.Lunge));

            Assert.That(session.Apply(new SetBodyMoveCommand(BodyMove.None), 2.0).Accepted, Is.True);
            Assert.That(session.CurrentPlan.BodyMove, Is.EqualTo(BodyMove.None));
        }

        [Test]
        public void SetBodyMove_RespectsTheConstraints_ButNoneIsAlwaysAllowed()
        {
            RulesSettings settings = GddStartingValues.CreateRulesSettings();
            var constraints = PlanningConstraints.CreateDefault(settings.Match);
            constraints.ForbidBodyMove(BodyMove.Jump);
            PlanningSession session = CreateSession(settings, constraints);

            Assert.That(session.Apply(new SetBodyMoveCommand(BodyMove.Jump), 1.0).Rejection,
                Is.EqualTo(CommandRejection.BodyMoveNotAllowed));
            Assert.That(session.CurrentPlan.BodyMove, Is.EqualTo(BodyMove.None));
            Assert.That(session.Apply(new SetBodyMoveCommand(BodyMove.None), 1.0).Accepted, Is.True);
            Assert.That(session.Apply(new SetBodyMoveCommand(BodyMove.Crouch), 1.0).Accepted, Is.True);
        }

        [Test]
        public void SetBodyMove_IsStillAllowedInTheLockout()
        {
            PlanningSession session = CreateSession();

            CommandResult result = session.Apply(new SetBodyMoveCommand(BodyMove.StepBack), DeadlineSeconds - JustBefore);

            Assert.That(result.Accepted, Is.True, "the lock-out targets weapon switching only");
        }

        [Test]
        public void BodyMovesAndPaths_StayOutOfThePublicState()
        {
            PlanningSession session = CreateSession();
            PublicPlanningState before = session.PublicState;

            session.Apply(new SetBodyMoveCommand(BodyMove.Lunge), 1.0);
            session.Apply(new SetPathCommand(Thrust(400f)), 2.0);

            Assert.That(session.PublicState, Is.EqualTo(before), "§3: body moves and paths are secret until execution");
        }

        // --- Paths and ink (§6) ----------------------------------------------------------------

        [Test]
        public void SetPath_StartsAtTheWeaponTip_BecauseOfTheLeadIn()
        {
            PlanningSession session = CreateSession();

            Assert.That(session.Apply(new SetPathCommand(Thrust(400f)), 1.0).Accepted, Is.True);

            WeaponPath path = session.CurrentPlan.Path;
            Assert.That(path.Points[0], Is.EqualTo(Tip), "D3: the lead-in starts at the weapon tip");
            Assert.That(Vec2.Distance(End(path), new Vec2(400f, 100f)), Is.LessThan(PathTolerance));
            Assert.That(path.LengthUnits, Is.EqualTo(300f).Within(PathTolerance), "50 lead-in + 250 drawn");
        }

        [Test]
        public void SetPath_PastTheInkBudget_IsCutWhereTheInkRunsOut_NotRefused()
        {
            RulesSettings settings = GddStartingValues.CreateRulesSettings();
            PlanningSession session = CreateSession(settings);

            CommandResult result = session.Apply(new SetPathCommand(ThereAndBackAgain()), 1.0);

            Assert.That(result.Accepted, Is.True);
            Assert.That(session.CurrentPath.Inked.WasCut, Is.True);
            Assert.That(session.CurrentPlan.Path.LengthUnits,
                Is.EqualTo(settings.FindWeapon(WeaponIds.Rapier).InkLengthUnits).Within(PathTolerance));
        }

        [Test]
        public void SetPath_UsesTheInkOfTheCurrentWeapon()
        {
            PlanningSession session = CreateSession();
            session.Apply(new SelectWeaponCommand(WeaponIds.Mace), 1.0);

            session.Apply(new SetPathCommand(ThereAndBackAgain()), 2.0);

            Assert.That(session.CurrentPlan.Path.LengthUnits, Is.EqualTo(200f).Within(PathTolerance), "mace: 200 ink");
        }

        [Test]
        public void SetPath_ScalesTheInkByTheConstraints()
        {
            RulesSettings settings = GddStartingValues.CreateRulesSettings();
            var constraints = PlanningConstraints.CreateDefault(settings.Match);
            constraints.InkLengthMultiplier = 0.5f;
            PlanningSession session = CreateSession(settings, constraints);

            session.Apply(new SetPathCommand(Thrust(600f)), 1.0);

            Assert.That(session.CurrentPlan.Path.LengthUnits, Is.EqualTo(300f).Within(PathTolerance), "half of the rapier's 600");
            Assert.That(settings.FindWeapon(WeaponIds.Rapier).InkLengthUnits, Is.EqualTo(600f), "the shared weapon data is untouched");
        }

        [Test]
        public void SetPath_WithNoInkLeft_IsRefused()
        {
            RulesSettings settings = GddStartingValues.CreateRulesSettings();
            var constraints = PlanningConstraints.CreateDefault(settings.Match);
            constraints.InkLengthMultiplier = 0f;
            PlanningSession session = CreateSession(settings, constraints);

            CommandResult result = session.Apply(new SetPathCommand(Thrust(400f)), 1.0);

            Assert.That(result.Rejection, Is.EqualTo(CommandRejection.InkBudgetExceeded));
            Assert.That(session.CurrentPlan.Path.IsEmpty, Is.True);
        }

        [Test]
        public void SetPath_ANewStrokeReplacesTheOldOne()
        {
            PlanningSession session = CreateSession();
            session.Apply(new SetPathCommand(Thrust(500f)), 1.0);

            session.Apply(new SetPathCommand(Thrust(300f)), 2.0);

            Assert.That(Vec2.Distance(End(session.CurrentPlan.Path), new Vec2(300f, 100f)), Is.LessThan(PathTolerance), "D5");
        }

        [Test]
        public void SetPath_WithoutPoints_IsRefused_AndKeepsThePreviousPath()
        {
            PlanningSession session = CreateSession();
            session.Apply(new SetPathCommand(Thrust(400f)), 1.0);

            CommandResult empty = session.Apply(new SetPathCommand(WeaponPath.Empty), 2.0);
            CommandResult missing = session.Apply(new SetPathCommand(null), 2.0);

            Assert.That(empty.Rejection, Is.EqualTo(CommandRejection.InvalidPath));
            Assert.That(missing.Rejection, Is.EqualTo(CommandRejection.InvalidPath));
            Assert.That(Vec2.Distance(End(session.CurrentPlan.Path), new Vec2(400f, 100f)), Is.LessThan(PathTolerance),
                "the earlier stroke is still the path");
        }

        [Test]
        public void SetPath_OnTheWeaponTip_AddsNoMovement_AndIsRefused()
        {
            PlanningSession session = CreateSession();

            CommandResult result = session.Apply(new SetPathCommand(new WeaponPath(new[] { Tip })), 1.0);

            Assert.That(result.Rejection, Is.EqualTo(CommandRejection.InvalidPath));
            Assert.That(session.CurrentPlan.Path.IsEmpty, Is.True);
        }

        [Test]
        public void ClearPath_RemovesThePath()
        {
            PlanningSession session = CreateSession();
            session.Apply(new SetPathCommand(Thrust(400f)), 1.0);

            Assert.That(session.Apply(new ClearPathCommand(), 2.0).Accepted, Is.True);

            Assert.That(session.CurrentPlan.Path.IsEmpty, Is.True);
        }

        // --- Drawing during the lock-out (D6, flag) --------------------------------------------

        [Test]
        public void Drawing_IsAllowedInTheLockoutByDefault()
        {
            PlanningSession session = CreateSession();

            Assert.That(session.Apply(new SetPathCommand(Thrust(400f)), LockoutStartSeconds + 0.5).Accepted, Is.True);
            Assert.That(session.Apply(new ClearPathCommand(), LockoutStartSeconds + 1.0).Accepted, Is.True);
        }

        [Test]
        public void Drawing_CanBeLockedOutByTheFlag()
        {
            RulesSettings settings = GddStartingValues.CreateRulesSettings();
            settings.Match.AllowDrawingDuringLockout = false;
            PlanningSession session = CreateSession(settings);

            Assert.That(session.Apply(new SetPathCommand(Thrust(400f)), LockoutStartSeconds - JustBefore).Accepted, Is.True);
            Assert.That(session.Apply(new SetPathCommand(Thrust(300f)), LockoutStartSeconds).Rejection,
                Is.EqualTo(CommandRejection.DrawingLockedOut));
            Assert.That(session.Apply(new ClearPathCommand(), LockoutStartSeconds).Rejection,
                Is.EqualTo(CommandRejection.DrawingLockedOut));
            Assert.That(session.CurrentPlan.Path.IsEmpty, Is.False, "the refused commands changed nothing");
        }

        // --- Signature moves (Session 15) ------------------------------------------------------

        [Test]
        public void UseSignature_IsRefusedUntilSession15()
        {
            PlanningSession session = CreateSession();

            CommandResult result = session.Apply(new UseSignatureCommand(new SignatureMoveId("head_impact")), 1.0);

            Assert.That(result.Rejection, Is.EqualTo(CommandRejection.SignatureUnavailable));
            Assert.That(session.CurrentPlan.Signature.IsEmpty, Is.True);
        }

        // --- Ready and cancel (§3, D9) ---------------------------------------------------------

        [Test]
        public void SetReady_IsRecorded_AndTheOpponentSeesIt()
        {
            PlanningSession session = CreateSession();
            Assert.That(session.PublicState.IsReady, Is.False);

            Assert.That(session.Apply(new SetReadyCommand(true), 5.0).Accepted, Is.True);

            Assert.That(session.IsReady, Is.True);
            Assert.That(session.CurrentPlan.PressedReady, Is.True);
            Assert.That(session.PublicState.IsReady, Is.True, "decided 2026-09-29: the opponent sees Ready");
        }

        [Test]
        public void SetReady_TwiceChangesNothing()
        {
            PlanningSession session = CreateSession();
            session.Apply(new SetReadyCommand(true), 5.0);

            Assert.That(session.Apply(new SetReadyCommand(true), 6.0).Accepted, Is.True);

            Assert.That(session.Audit.ReadySeconds, Is.EqualTo(5.0), "the first press counts");
        }

        [Test]
        public void ReadyCancel_IsAllowedBeforeTheLockoutByDefault()
        {
            PlanningSession session = CreateSession();
            session.Apply(new SetReadyCommand(true), 5.0);

            CommandResult result = session.Apply(new SetReadyCommand(false), LockoutStartSeconds - JustBefore);

            Assert.That(result.Accepted, Is.True);
            Assert.That(session.IsReady, Is.False);
            Assert.That(session.PublicState.IsReady, Is.False);
            Assert.That(session.Audit.ReadySeconds, Is.Null);
        }

        [Test]
        public void ReadyCancel_IsRefusedWhenTheFlagIsOff()
        {
            RulesSettings settings = GddStartingValues.CreateRulesSettings();
            settings.Match.AllowReadyCancel = false;
            PlanningSession session = CreateSession(settings);
            session.Apply(new SetReadyCommand(true), 5.0);

            CommandResult result = session.Apply(new SetReadyCommand(false), 6.0);

            Assert.That(result.Rejection, Is.EqualTo(CommandRejection.ReadyCancelNotAllowed));
            Assert.That(session.IsReady, Is.True);
        }

        [Test]
        public void ReadyCancel_ClosesWhenTheLockoutStarts()
        {
            PlanningSession session = CreateSession();
            session.Apply(new SetReadyCommand(true), 5.0);

            CommandResult result = session.Apply(new SetReadyCommand(false), LockoutStartSeconds);

            Assert.That(result.Rejection, Is.EqualTo(CommandRejection.ReadyCancelNotAllowed));
            Assert.That(session.IsReady, Is.True);
        }

        [Test]
        public void ReadyCancel_InTheLockout_CanBeAllowedByItsFlag()
        {
            RulesSettings settings = GddStartingValues.CreateRulesSettings();
            settings.Match.AllowReadyCancelDuringLockout = true;
            PlanningSession session = CreateSession(settings);
            session.Apply(new SetReadyCommand(true), 5.0);

            Assert.That(session.Apply(new SetReadyCommand(false), LockoutStartSeconds + 0.5).Accepted, Is.True);
        }

        [Test]
        public void CancellingWhenNotReady_ChangesNothing()
        {
            RulesSettings settings = GddStartingValues.CreateRulesSettings();
            settings.Match.AllowReadyCancel = false;
            PlanningSession session = CreateSession(settings);

            Assert.That(session.Apply(new SetReadyCommand(false), 1.0).Accepted, Is.True);
        }

        [Test]
        public void WhileReady_ThePlanIsFrozen_UntilReadyIsCancelled()
        {
            PlanningSession session = CreateSession();
            session.Apply(new SetReadyCommand(true), 5.0);

            Assert.That(session.Apply(new SelectWeaponCommand(WeaponIds.Mace), 6.0).Rejection, Is.EqualTo(CommandRejection.AlreadyReady));
            Assert.That(session.Apply(new SetBodyMoveCommand(BodyMove.Lunge), 6.0).Rejection, Is.EqualTo(CommandRejection.AlreadyReady));
            Assert.That(session.Apply(new SetPathCommand(Thrust(400f)), 6.0).Rejection, Is.EqualTo(CommandRejection.AlreadyReady));
            Assert.That(session.Apply(new ClearPathCommand(), 6.0).Rejection, Is.EqualTo(CommandRejection.AlreadyReady));
            Assert.That(session.Apply(new UseSignatureCommand(new SignatureMoveId("x")), 6.0).Rejection, Is.EqualTo(CommandRejection.AlreadyReady));

            session.Apply(new SetReadyCommand(false), 7.0);
            Assert.That(session.Apply(new SelectWeaponCommand(WeaponIds.Mace), 8.0).Accepted, Is.True);
        }

        // --- Timeout (§3, Decided) -------------------------------------------------------------

        [Test]
        public void Commands_AreAcceptedUpToTheDeadline_ThenRefused()
        {
            PlanningSession session = CreateSession();

            Assert.That(session.Apply(new SetBodyMoveCommand(BodyMove.Crouch), DeadlineSeconds - JustBefore).Accepted, Is.True);
            Assert.That(session.Apply(new SetBodyMoveCommand(BodyMove.Jump), DeadlineSeconds).Rejection,
                Is.EqualTo(CommandRejection.NotInPlanningPhase));
            Assert.That(session.Apply(new SetReadyCommand(true), DeadlineSeconds + 1.0).Rejection,
                Is.EqualTo(CommandRejection.NotInPlanningPhase));
        }

        [Test]
        public void Timeout_WhateverIsSetStillExecutes_EvenWithoutReady()
        {
            PlanningSession session = CreateSession();
            session.Apply(new SelectWeaponCommand(WeaponIds.Mace), 1.0);
            session.Apply(new SetBodyMoveCommand(BodyMove.Lunge), 2.0);
            session.Apply(new SetPathCommand(Thrust(250f)), 3.0);

            // Nobody pressed Ready: the timer ran out and the plan is read as it stands.
            session.Apply(new SetBodyMoveCommand(BodyMove.Crouch), DeadlineSeconds);

            TurnPlan plan = session.CurrentPlan;
            Assert.That(plan.Weapon, Is.EqualTo(WeaponIds.Mace));
            Assert.That(plan.BodyMove, Is.EqualTo(BodyMove.Lunge), "the late command did not count");
            Assert.That(plan.Path.IsEmpty, Is.False);
            Assert.That(plan.PressedReady, Is.False);
        }

        // --- The audit the validator re-checks (§18) -------------------------------------------

        [Test]
        public void Audit_RecordsWhenTheWeaponPathAndReadyChanged()
        {
            PlanningSession session = CreateSession();
            Assert.That(session.Audit.LastWeaponSwitchSeconds, Is.Null);

            session.Apply(new SelectWeaponCommand(WeaponIds.Mace), 2.0);
            session.Apply(new SetPathCommand(Thrust(250f)), 3.0);
            session.Apply(new SetReadyCommand(true), 4.0);

            PlanningAudit audit = session.Audit;
            Assert.That(audit.DeadlineSeconds, Is.EqualTo(DeadlineSeconds));
            Assert.That(audit.LastWeaponSwitchSeconds, Is.EqualTo(2.0));
            Assert.That(audit.LastPathChangeSeconds, Is.EqualTo(3.0));
            Assert.That(audit.ReadySeconds, Is.EqualTo(4.0));
        }

        [Test]
        public void Audit_IgnoresRefusedCommands()
        {
            PlanningSession session = CreateSession();

            session.Apply(new SelectWeaponCommand(WeaponIds.Mace), LockoutStartSeconds);

            Assert.That(session.Audit.LastWeaponSwitchSeconds, Is.Null);
        }

        // --- Seam defaults ---------------------------------------------------------------------

        [Test]
        public void SeamDefaults_AreTheRecommendedOnes()
        {
            var match = new MatchSettings();

            Assert.That(match.AllowDrawingDuringLockout, Is.True, "D6: drawing is allowed in the lock-out");
            Assert.That(match.AllowReadyCancel, Is.True, "D9: Ready can be cancelled...");
            Assert.That(match.AllowReadyCancelDuringLockout, Is.False, "D9: ...until the lock-out starts");
        }
    }
}
