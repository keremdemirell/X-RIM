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
    /// The authority re-checks a locked plan that holds a signature move (GDD §8; §18: "the server should check every input"): the move,
    /// the charge, the weapon tie, the move's own body move and that the path is exactly the pattern.
    /// </summary>
    public sealed class PlanValidatorSignatureTests
    {
        private RulesSettings _settings;
        private PlanningConstraints _constraints;
        private PlanValidator _validator;
        private FixedWeaponTipSource _tips;

        [SetUp]
        public void SetUp()
        {
            _settings = SignatureTestData.CreateSettings();
            _constraints = PlanningConstraints.CreateDefault(_settings.Match);
            _validator = new PlanValidator(_settings, new RulePolicies());
            _tips = new FixedWeaponTipSource(Tip);
        }

        private SignaturePlanContext Context(bool charged = true, IReadOnlyList<SignatureMoveStats> moves = null) =>
            new SignaturePlanContext(charged ? SignatureTestData.Charged : new SignatureChargeState(false, 0f),
                moves ?? SignatureCatalogue.Brought(_settings, null), _tips);

        /// <summary>The plan a session makes when the player taps the move.</summary>
        private TurnPlan PlanOf(SignatureMoveId id, WeaponId? weapon = null)
        {
            PlanningSession session = CreateSession(_settings, _constraints, signatureCharge: SignatureTestData.Charged);
            if (weapon.HasValue) session.Apply(new SelectWeaponCommand(weapon.Value), 0.5);
            Assert.That(session.Apply(new UseSignatureCommand(id), 1.0).Accepted, Is.True, "the session accepts the move");
            return session.CurrentPlan;
        }

        private CommandResult Check(TurnPlan plan, SignaturePlanContext context) =>
            _validator.Validate(plan, new PlanningAudit(DeadlineSeconds, null, 1.0, null), Loadout, _constraints, context);

        [Test]
        public void ASessionsSignaturePlan_IsValid()
        {
            PlanningSession session = CreateSession(_settings, _constraints, signatureCharge: SignatureTestData.Charged);
            session.Apply(new UseSignatureCommand(SignatureTestData.Sweep), 1.0);
            session.Apply(new SetReadyCommand(true), 2.0);

            Assert.That(_validator.Validate(session).Accepted, Is.True);
        }

        [Test]
        public void AMoveThatIgnoresInk_IsNotRejectedForItsLongPath()
        {
            TurnPlan plan = PlanOf(SignatureTestData.Sweep);

            Assert.That(plan.Path.LengthUnits, Is.GreaterThan(_settings.FindWeapon(WeaponIds.Rapier).InkLengthUnits));
            Assert.That(Check(plan, Context()).Accepted, Is.True);
        }

        [Test]
        public void WithoutTheContext_EverySignaturePlanIsRefused()
        {
            TurnPlan plan = PlanOf(SignatureTestData.Jab);

            Assert.That(Check(plan, null).Rejection, Is.EqualTo(CommandRejection.SignatureUnavailable));
        }

        [Test]
        public void AMoveNotInTheBroughtList_IsRefused()
        {
            TurnPlan plan = PlanOf(SignatureTestData.Jab);

            CommandResult result = Check(plan, Context(moves: new[] { SignatureTestData.CreateSweep() }));

            Assert.That(result.Rejection, Is.EqualTo(CommandRejection.SignatureUnavailable));
        }

        [Test]
        public void ANotChargedDummy_IsRefused()
        {
            TurnPlan plan = PlanOf(SignatureTestData.Jab);

            Assert.That(Check(plan, Context(charged: false)).Rejection, Is.EqualTo(CommandRejection.SignatureNotCharged));
        }

        [Test]
        public void SignatureMovesSwitchedOffForTheTurn_AreRefused()
        {
            TurnPlan plan = PlanOf(SignatureTestData.Jab);
            _constraints.CanUseSignature = false;

            Assert.That(Check(plan, Context()).Rejection, Is.EqualTo(CommandRejection.SignatureUnavailable));
        }

        [Test]
        public void ATiedMoveWithAnotherWeapon_IsRefused()
        {
            TurnPlan withMace = PlanOf(SignatureTestData.MaceOnly, WeaponIds.Mace);
            var withRapier = new TurnPlan(WeaponIds.Rapier, withMace.BodyMove, withMace.Path, withMace.Signature, false);

            Assert.That(Check(withMace, Context()).Accepted, Is.True);
            Assert.That(Check(withRapier, Context()).Rejection, Is.EqualTo(CommandRejection.SignatureWrongWeapon));
        }

        [Test]
        public void APathThatIsNotThePattern_IsRefused()
        {
            TurnPlan real = PlanOf(SignatureTestData.Jab);
            var forged = new TurnPlan(real.Weapon, real.BodyMove, Thrust(600f), real.Signature, false);

            Assert.That(Check(forged, Context()).Rejection, Is.EqualTo(CommandRejection.InvalidPath));
        }

        [Test]
        public void ASignaturePlanWithNoPath_IsRefused()
        {
            var plan = new TurnPlan(WeaponIds.Rapier, BodyMove.None, WeaponPath.Empty, SignatureTestData.Jab, false);

            Assert.That(Check(plan, Context()).Rejection, Is.EqualTo(CommandRejection.InvalidPath));
        }

        [Test]
        public void AMoveWithItsOwnBodyMove_NeedsThatBodyMoveInThePlan()
        {
            TurnPlan real = PlanOf(SignatureTestData.LungeJab);
            var swapped = new TurnPlan(real.Weapon, BodyMove.Crouch, real.Path, real.Signature, false);

            Assert.That(real.BodyMove, Is.EqualTo(BodyMove.Lunge));
            Assert.That(Check(real, Context()).Accepted, Is.True);
            Assert.That(Check(swapped, Context()).Rejection, Is.EqualTo(CommandRejection.BodyMoveNotAllowed));
        }

        [Test]
        public void TheOtherChecksStillApply_ALateDrawIsRefusedEvenForAMove()
        {
            TurnPlan plan = PlanOf(SignatureTestData.Jab);
            var late = new PlanningAudit(DeadlineSeconds, null, DeadlineSeconds + 0.1, null);

            CommandResult result = _validator.Validate(plan, late, Loadout, _constraints, Context());

            Assert.That(result.Rejection, Is.EqualTo(CommandRejection.NotInPlanningPhase));
        }
    }
}
