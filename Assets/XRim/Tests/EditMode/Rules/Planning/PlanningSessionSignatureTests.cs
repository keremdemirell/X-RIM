using System.Collections.Generic;
using NUnit.Framework;
using XRim.Core;
using XRim.Rules;
using XRim.Rules.Paths;
using XRim.Rules.Planning;
using XRim.Rules.Settings;
using XRim.Rules.Signature;
using static XRim.Tests.EditMode.Rules.Planning.PlanningTestData;

namespace XRim.Tests.EditMode.Rules.Planning
{
    /// <summary>
    /// Signature moves in planning (GDD §8, TBD; designer 2026-10-15: "all recommended"): a tap sets the preset pattern as the path, the
    /// move replaces only the weapon path, ignores ink by default, works with any weapon, stays hidden from the opponent, and is cancelled
    /// by drawing, clearing or switching weapon. The test catalogue is <see cref="SignatureTestData"/>; the weapon tip rests at (100, 100).
    /// </summary>
    public sealed class PlanningSessionSignatureTests
    {
        private const double Now = 1.0;
        private const float Tolerance = 1f;
        private const float RapierInkUnits = 600f;

        private RulesSettings _settings;
        private PlanningConstraints _constraints;

        [SetUp]
        public void SetUp()
        {
            _settings = SignatureTestData.CreateSettings();
            _constraints = PlanningConstraints.CreateDefault(_settings.Match);
        }

        private PlanningSession Session(SignatureChargeState? charge = null, IReadOnlyList<SignatureMoveStats> brought = null) =>
            CreateSession(_settings, _constraints, signatureCharge: charge ?? SignatureTestData.Charged, signatures: brought);

        private static CommandResult Use(PlanningSession session, SignatureMoveId id, double now = Now) =>
            session.Apply(new UseSignatureCommand(id), now);

        // --- Choosing a move ---------------------------------------------------------------------

        [Test]
        public void UseSignature_PutsThePatternOnThePlan_AsThePath()
        {
            PlanningSession session = Session();

            CommandResult result = Use(session, SignatureTestData.Jab);

            Assert.That(result.Accepted, Is.True);
            TurnPlan plan = session.CurrentPlan;
            Assert.That(plan.Signature, Is.EqualTo(SignatureTestData.Jab));
            Assert.That(plan.Path.IsEmpty, Is.False);
            Assert.That(plan.Path.Points[0], Is.EqualTo(Tip), "the lead-in starts at the weapon tip (D3)");
            Assert.That(End(plan.Path).X, Is.EqualTo(350f).Within(0.01f));
            Assert.That(session.SelectedSignature, Is.EqualTo(SignatureTestData.Jab));
            Assert.That(session.CurrentPath, Is.Not.Null, "the preview reads it like a drawn path");
        }

        [Test]
        public void UseSignature_IsAJokerPatternThePlayerNeverDrew_SoItCountsAsAPathChangeForTheAudit()
        {
            PlanningSession session = Session();

            Use(session, SignatureTestData.Jab, 3.5);

            Assert.That(session.Audit.LastPathChangeSeconds, Is.EqualTo(3.5));
        }

        [Test]
        public void ChoosingTheSameMoveAgain_SetsTheSamePattern()
        {
            PlanningSession session = Session();
            Use(session, SignatureTestData.Jab);
            WeaponPath first = session.CurrentPlan.Path;

            Assert.That(Use(session, SignatureTestData.Jab, 2.0).Accepted, Is.True);

            Assert.That(session.CurrentPlan.Path.Points.Count, Is.EqualTo(first.Points.Count));
            Assert.That(session.CurrentPlan.Signature, Is.EqualTo(SignatureTestData.Jab));
        }

        [Test]
        public void ChoosingAnotherMove_ReplacesTheFirst()
        {
            PlanningSession session = Session();
            Use(session, SignatureTestData.Jab);

            Use(session, SignatureTestData.Sweep);

            Assert.That(session.CurrentPlan.Signature, Is.EqualTo(SignatureTestData.Sweep));
        }

        [Test]
        public void AMove_ReplacesADrawnPath()
        {
            PlanningSession session = Session();
            session.Apply(new SetPathCommand(Thrust(500f)), 0.5);

            Use(session, SignatureTestData.Jab);

            Assert.That(End(session.CurrentPlan.Path).X, Is.EqualTo(350f).Within(0.01f), "the pattern, not the stroke");
        }

        // --- Secrecy (§8) --------------------------------------------------------------------------

        [Test]
        public void TheChosenMove_StaysHiddenFromTheOpponent()
        {
            _settings.Signature.OpponentSeesCharged = true;
            PlanningSession session = Session();
            PublicPlanningState before = session.PublicState;

            Use(session, SignatureTestData.Jab);

            Assert.That(session.PublicState, Is.EqualTo(before), "choosing a move changes nothing the opponent sees");
            Assert.That(before.SignatureChargedVisible, Is.True, "a charged move is visible only because the flag allows it");
        }

        [Test]
        public void ACharge_IsHiddenByDefault_AndShownWhenTheFlagAllowsIt()
        {
            Assert.That(Session().PublicState.SignatureChargedVisible, Is.False);

            _settings.Signature.OpponentSeesCharged = true;
            Assert.That(Session().PublicState.SignatureChargedVisible, Is.True);
        }

        // --- What refuses a move -----------------------------------------------------------------------

        [Test]
        public void ANotChargedDummy_CannotUseAMove()
        {
            PlanningSession session = Session(new SignatureChargeState(false, 0.3f));

            CommandResult result = Use(session, SignatureTestData.Jab);

            Assert.That(result.Rejection, Is.EqualTo(CommandRejection.SignatureNotCharged));
            Assert.That(session.CurrentPlan.Signature.IsEmpty, Is.True);
            Assert.That(session.CanUseSignature, Is.False);
        }

        [Test]
        public void AMoveTheDummyDidNotBring_IsRefused()
        {
            PlanningSession session = Session(brought: new[] { SignatureTestData.CreateSweep() });

            CommandResult result = Use(session, SignatureTestData.Jab);

            Assert.That(result.Rejection, Is.EqualTo(CommandRejection.SignatureUnavailable));
        }

        [Test]
        public void SwitchedOffForTheTurn_EveryMoveIsRefused()
        {
            _constraints.CanUseSignature = false;
            PlanningSession session = Session();

            CommandResult result = Use(session, SignatureTestData.Jab);

            Assert.That(result.Rejection, Is.EqualTo(CommandRejection.SignatureUnavailable));
            Assert.That(session.CanUseSignature, Is.False);
        }

        [Test]
        public void ARefusedMove_ChangesNothing_NotEvenADrawnPath()
        {
            PlanningSession session = Session(new SignatureChargeState(false, 0f));
            session.Apply(new SetPathCommand(Thrust(500f)), 0.5);
            WeaponPath drawn = session.CurrentPlan.Path;

            Use(session, SignatureTestData.Jab);

            Assert.That(session.CurrentPlan.Path.Points.Count, Is.EqualTo(drawn.Points.Count));
            Assert.That(session.Audit.LastPathChangeSeconds, Is.EqualTo(0.5));
        }

        [Test]
        public void AfterTheDeadline_ACommandIsRefused()
        {
            PlanningSession session = Session();

            Assert.That(Use(session, SignatureTestData.Jab, DeadlineSeconds).Rejection, Is.EqualTo(CommandRejection.NotInPlanningPhase));
        }

        [Test]
        public void WhileReady_TheMoveCannotBeChanged()
        {
            PlanningSession session = Session();
            session.Apply(new SetReadyCommand(true), 1.0);

            Assert.That(Use(session, SignatureTestData.Jab, 2.0).Rejection, Is.EqualTo(CommandRejection.AlreadyReady));
        }

        [Test]
        public void TapsFollowTheDrawingLockout_AllowedByDefault()
        {
            PlanningSession session = Session();

            Assert.That(Use(session, SignatureTestData.Jab, LockoutStartSeconds + 0.5).Accepted, Is.True, "D6: drawing is allowed in the lock-out");
        }

        [Test]
        public void TapsFollowTheDrawingLockout_WhenDrawingIsLockedOut()
        {
            _settings.Match.AllowDrawingDuringLockout = false;
            PlanningSession session = Session();

            Assert.That(Use(session, SignatureTestData.Jab, LockoutStartSeconds - 0.001).Accepted, Is.True);
            Assert.That(Use(session, SignatureTestData.Sweep, LockoutStartSeconds).Rejection, Is.EqualTo(CommandRejection.DrawingLockedOut));
            Assert.That(session.CurrentPlan.Signature, Is.EqualTo(SignatureTestData.Jab), "the refused tap changed nothing");
        }

        // --- Weapon ties (§8, TBD: any weapon) ---------------------------------------------------------

        [Test]
        public void ByDefault_AMoveWorksWithAnyWeapon()
        {
            PlanningSession session = Session();
            session.Apply(new SelectWeaponCommand(WeaponIds.Shield), 0.5);

            Assert.That(Use(session, SignatureTestData.Jab).Accepted, Is.True);
        }

        [Test]
        public void ATiedMove_NeedsItsWeapon()
        {
            PlanningSession session = Session();

            Assert.That(Use(session, SignatureTestData.MaceOnly).Rejection, Is.EqualTo(CommandRejection.SignatureWrongWeapon));

            session.Apply(new SelectWeaponCommand(WeaponIds.Mace), 0.5);
            Assert.That(Use(session, SignatureTestData.MaceOnly).Accepted, Is.True);
        }

        // --- Ink (§8, TBD: ignores ink) ---------------------------------------------------------------------

        [Test]
        public void ByDefault_AMoveIgnoresTheInk_SoTheWholePatternPlays()
        {
            PlanningSession session = Session();

            Use(session, SignatureTestData.Sweep);

            Assert.That(session.CurrentPlan.Path.LengthUnits, Is.GreaterThan(RapierInkUnits * 2f), "three times a rapier's whole ink");
            Assert.That(session.CurrentPath.Inked.WasCut, Is.False);
        }

        [Test]
        public void AMoveThatPaysInk_IsCutWhereTheInkRunsOut()
        {
            PlanningSession session = Session();

            Use(session, SignatureTestData.PaidSweep);

            Assert.That(session.CurrentPlan.Path.LengthUnits, Is.EqualTo(RapierInkUnits).Within(Tolerance));
            Assert.That(session.CurrentPath.Inked.WasCut, Is.True);
        }

        [Test]
        public void AMoveThatPaysInk_FollowsTheTurnsInkLimit()
        {
            _constraints.InkLengthMultiplier = 0.5f;
            PlanningSession session = Session();

            Use(session, SignatureTestData.PaidSweep);

            Assert.That(session.CurrentPlan.Path.LengthUnits, Is.EqualTo(RapierInkUnits * 0.5f).Within(Tolerance));
        }

        [Test]
        public void AMoveStillStopsAtTheReachLimit()
        {
            PlanningSession session = Session();
            session.Apply(new SelectWeaponCommand(WeaponIds.Mace), 0.5); // arm 240 + mace 220: 460 from the shoulder

            Use(session, SignatureTestData.Sweep);

            foreach (Vec2 point in session.CurrentPlan.Path.Points)
            {
                Assert.That(point.X, Is.LessThanOrEqualTo(460.1f), "D4: clamped to the weapon's reach");
            }
        }

        // --- Body moves (§8, TBD: the move replaces only the weapon path) ------------------------------

        [Test]
        public void ABodySwipe_StaysAvailable_BeforeAndAfterAMove()
        {
            PlanningSession session = Session();
            session.Apply(new SetBodyMoveCommand(BodyMove.Crouch), 0.5);

            Use(session, SignatureTestData.Jab);
            Assert.That(session.CurrentPlan.BodyMove, Is.EqualTo(BodyMove.Crouch), "choosing a move keeps the swipe");

            Assert.That(session.Apply(new SetBodyMoveCommand(BodyMove.Lunge), 2.0).Accepted, Is.True);
            Assert.That(session.CurrentPlan.BodyMove, Is.EqualTo(BodyMove.Lunge));
            Assert.That(session.CurrentPlan.Signature, Is.EqualTo(SignatureTestData.Jab), "swiping does not cancel the move");
        }

        [Test]
        public void AMoveThatDefinesItsOwnBodyMove_RunsWithIt_AndLeavesNoSwipe()
        {
            PlanningSession session = Session();
            session.Apply(new SetBodyMoveCommand(BodyMove.Crouch), 0.5);

            Use(session, SignatureTestData.LungeJab);

            Assert.That(session.CurrentPlan.BodyMove, Is.EqualTo(BodyMove.Lunge));
            Assert.That(session.Apply(new SetBodyMoveCommand(BodyMove.Jump), 2.0).Rejection, Is.EqualTo(CommandRejection.BodyMoveNotAllowed));

            session.Apply(new ClearPathCommand(), 3.0);
            Assert.That(session.CurrentPlan.BodyMove, Is.EqualTo(BodyMove.Crouch), "cancelling the move gives the swipe back");
            Assert.That(session.Apply(new SetBodyMoveCommand(BodyMove.Jump), 4.0).Accepted, Is.True);
        }

        [Test]
        public void AMoveWhoseOwnBodyMoveIsForbidden_IsRefused()
        {
            _constraints.ForbidBodyMove(BodyMove.Lunge);
            PlanningSession session = Session();

            CommandResult result = Use(session, SignatureTestData.LungeJab);

            Assert.That(result.Rejection, Is.EqualTo(CommandRejection.BodyMoveNotAllowed));
            Assert.That(session.CurrentPlan.Signature.IsEmpty, Is.True);
        }

        // --- Cancelling (§6, §8) ----------------------------------------------------------------------

        [Test]
        public void ClearingThePath_CancelsTheMove()
        {
            PlanningSession session = Session();
            Use(session, SignatureTestData.Jab);

            Assert.That(session.Apply(new ClearPathCommand(), 2.0).Accepted, Is.True);

            Assert.That(session.CurrentPlan.Signature.IsEmpty, Is.True);
            Assert.That(session.CurrentPlan.Path.IsEmpty, Is.True);
        }

        [Test]
        public void ARedraw_CancelsTheMove_AndStartsFromTheNewStroke()
        {
            PlanningSession session = Session();
            Use(session, SignatureTestData.Sweep);

            session.Apply(new SetPathCommand(Thrust(300f)), 2.0);

            Assert.That(session.CurrentPlan.Signature.IsEmpty, Is.True);
            Assert.That(End(session.CurrentPlan.Path).X, Is.EqualTo(300f).Within(0.01f));
            Assert.That(session.CurrentPlan.Path.LengthUnits, Is.LessThan(500f), "the pattern is not drawn on");
        }

        [Test]
        public void SwitchingWeapon_ErasesTheMove_LikeADrawnPath()
        {
            PlanningSession session = Session();
            Use(session, SignatureTestData.Jab);

            session.Apply(new SelectWeaponCommand(WeaponIds.Mace), 2.0);

            Assert.That(session.CurrentPlan.Signature.IsEmpty, Is.True);
            Assert.That(session.CurrentPlan.Path.IsEmpty, Is.True);
        }

        [Test]
        public void ARefusedRedraw_KeepsTheMove()
        {
            PlanningSession session = Session();
            Use(session, SignatureTestData.Jab);

            session.Apply(new SetPathCommand(new WeaponPath(new[] { Tip })), 2.0); // one point on the tip: not a path

            Assert.That(session.CurrentPlan.Signature, Is.EqualTo(SignatureTestData.Jab));
        }

        // --- The placeholder catalogue (designer 2026-10-15) -------------------------------------------------

        [Test]
        public void EveryPlaceholderMove_PlaysWithEveryLoadoutWeapon()
        {
            RulesSettings settings = GddStartingValues.CreateRulesSettings();
            SignatureMoveId[] moves =
            {
                SignatureMoveIds.FiveFingerExplodingTouch, SignatureMoveIds.HeadImpactSimulation404, SignatureMoveIds.WhiplashCalibrationSpin,
            };

            foreach (WeaponId weapon in Loadout)
            {
                foreach (SignatureMoveId move in moves)
                {
                    PlanningSession session = CreateSession(settings, signatureCharge: SignatureTestData.Charged);
                    session.Apply(new SelectWeaponCommand(weapon), 0.5);

                    CommandResult result = Use(session, move);

                    Assert.That(result.Accepted, Is.True, $"{move} with {weapon}: {result.Rejection}");
                    Assert.That(session.CurrentPlan.Path.IsEmpty, Is.False, $"{move} with {weapon}");
                }
            }
        }

        // --- The move is a move (D10) ----------------------------------------------------------------------

        [Test]
        public void AMoveAlone_IsNotAnIdlePlan()
        {
            PlanningSession session = Session();
            Use(session, SignatureTestData.Jab);

            Assert.That(new AnyMoveIdleTurnPolicy().IsIdle(Side.Left, session.CurrentPlan), Is.False);
        }
    }
}
