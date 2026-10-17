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
    /// <summary>The default policies behind the planning TBD seams (D10, public state) and their wiring.</summary>
    public sealed class PlanningPolicyTests
    {
        private static readonly SignatureMoveId SomeSignature = new SignatureMoveId("head_impact");

        // --- D10: which plans count as idle ----------------------------------------------------

        [Test]
        public void IdleTurn_APlanWithNothingSetIsIdle()
        {
            Assert.That(new AnyMoveIdleTurnPolicy().IsIdle(TurnPlan.Empty(WeaponIds.Rapier)), Is.True);
        }

        [TestCase(BodyMove.Crouch)]
        [TestCase(BodyMove.Lunge)]
        [TestCase(BodyMove.StepBack)]
        [TestCase(BodyMove.Jump)]
        public void IdleTurn_AnyBodyMoveIsAMove(BodyMove move)
        {
            var plan = new TurnPlan(WeaponIds.Rapier, move, WeaponPath.Empty, default, false);

            Assert.That(new AnyMoveIdleTurnPolicy().IsIdle(plan), Is.False);
        }

        [Test]
        public void IdleTurn_ADrawnPathIsAMove()
        {
            var plan = new TurnPlan(WeaponIds.Rapier, BodyMove.None, Thrust(400f), default, false);

            Assert.That(new AnyMoveIdleTurnPolicy().IsIdle(plan), Is.False);
        }

        [Test]
        public void IdleTurn_ASignatureMoveIsAMove()
        {
            var plan = new TurnPlan(WeaponIds.Rapier, BodyMove.None, WeaponPath.Empty, SomeSignature, false);

            Assert.That(new AnyMoveIdleTurnPolicy().IsIdle(plan), Is.False);
        }

        [Test]
        public void IdleTurn_AnotherWeaponAndReadyAloneAreNotAMove()
        {
            var plan = new TurnPlan(WeaponIds.Mace, BodyMove.None, WeaponPath.Empty, default, true);

            Assert.That(new AnyMoveIdleTurnPolicy().IsIdle(plan), Is.True);
        }

        // --- Public state ----------------------------------------------------------------------

        [Test]
        public void PublicState_ShowsTheWeaponAndReady()
        {
            PlanningSession session = CreateSession();
            session.Apply(new SelectWeaponCommand(WeaponIds.Shield), 1.0);
            session.Apply(new SetReadyCommand(true), 2.0);

            PublicPlanningState state = new DefaultPublicStatePolicy().Build(session, false);

            Assert.That(state.Weapon, Is.EqualTo(WeaponIds.Shield));
            Assert.That(state.IsReady, Is.True);
            Assert.That(state.SignatureChargedVisible, Is.False);
        }

        [Test]
        public void PublicState_ACharge_IsHiddenUnlessTheSettingAllowsIt()
        {
            RulesSettings hidden = SignatureTestData.CreateSettings();
            RulesSettings shown = SignatureTestData.CreateSettings();
            shown.Signature.OpponentSeesCharged = true;
            var policy = new DefaultPublicStatePolicy();

            Assert.That(policy.Build(CreateSession(hidden), true).SignatureChargedVisible, Is.False);
            Assert.That(policy.Build(CreateSession(shown), true).SignatureChargedVisible, Is.True);
            Assert.That(policy.Build(CreateSession(shown), false).SignatureChargedVisible, Is.False, "nothing charged, nothing to show");
        }

        [Test]
        public void PublicState_AChargeComesFromTheChargeTheSessionWasOpenedWith()
        {
            RulesSettings settings = SignatureTestData.CreateSettings();
            settings.Signature.OpponentSeesCharged = true;

            PlanningSession charged = CreateSession(settings, signatureCharge: new SignatureChargeState(true, 1f));
            PlanningSession empty = CreateSession(settings, signatureCharge: new SignatureChargeState(false, 0.4f));
            PlanningSession unknown = CreateSession(settings);

            Assert.That(charged.PublicState.SignatureChargedVisible, Is.True);
            Assert.That(empty.PublicState.SignatureChargedVisible, Is.False);
            Assert.That(unknown.PublicState.SignatureChargedVisible, Is.False, "no charge state, nothing charged");
        }

        [Test]
        public void PublicState_AChargeThatCannotBeUsedThisTurn_IsNotShown()
        {
            RulesSettings settings = SignatureTestData.CreateSettings();
            settings.Signature.OpponentSeesCharged = true;
            PlanningConstraints noSignatures = PlanningConstraints.CreateDefault(settings.Match);
            noSignatures.CanUseSignature = false;

            PlanningSession session = CreateSession(settings, noSignatures, signatureCharge: SignatureTestData.Charged);

            Assert.That(session.PublicState.SignatureChargedVisible, Is.False, "§14: switched off in sudden death, so nothing is ready");
        }

        [Test]
        public void PublicState_IsComparable()
        {
            var a = new PublicPlanningState(WeaponIds.Rapier, true, false);

            Assert.That(a, Is.EqualTo(new PublicPlanningState(WeaponIds.Rapier, true, false)));
            Assert.That(a, Is.Not.EqualTo(new PublicPlanningState(WeaponIds.Mace, true, false)));
            Assert.That(a, Is.Not.EqualTo(new PublicPlanningState(WeaponIds.Rapier, false, false)));
            Assert.That(a, Is.Not.EqualTo(new PublicPlanningState(WeaponIds.Rapier, true, true)));
        }

        // --- Wiring ----------------------------------------------------------------------------

        [Test]
        public void RulePolicies_DefaultsAreWired()
        {
            var policies = new RulePolicies();

            Assert.That(policies.IdleTurn, Is.InstanceOf<AnyMoveIdleTurnPolicy>());
            Assert.That(policies.PublicState, Is.InstanceOf<DefaultPublicStatePolicy>());
            Assert.That(policies.SignatureCharge, Is.InstanceOf<SettingsSignatureChargeModel>());
        }

        [Test]
        public void WeaponStats_InkMultiplier_ScalesACopy_AndLeavesTheOriginal()
        {
            WeaponStats rapier = GddStartingValues.Rapier();

            Assert.That(rapier.WithInkLengthMultiplier(1f), Is.SameAs(rapier));

            WeaponStats halved = rapier.WithInkLengthMultiplier(0.5f);
            Assert.That(halved, Is.Not.SameAs(rapier));
            Assert.That(halved.InkLengthUnits, Is.EqualTo(300f));
            Assert.That(halved.Id, Is.EqualTo(rapier.Id));
            Assert.That(halved.SpeedUnitsPerSecond, Is.EqualTo(rapier.SpeedUnitsPerSecond));
            Assert.That(rapier.InkLengthUnits, Is.EqualTo(600f));
        }
    }
}
