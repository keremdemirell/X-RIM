using NUnit.Framework;
using XRim.Core;
using XRim.Rules;
using XRim.Rules.Paths;
using XRim.Rules.Planning;
using XRim.Rules.Settings;
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
            RulesSettings hidden = GddStartingValues.CreateRulesSettings();
            RulesSettings shown = GddStartingValues.CreateRulesSettings();
            shown.Signature.OpponentSeesCharged = true;
            var policy = new DefaultPublicStatePolicy();

            Assert.That(policy.Build(CreateSession(hidden), true).SignatureChargedVisible, Is.False);
            Assert.That(policy.Build(CreateSession(shown), true).SignatureChargedVisible, Is.True);
            Assert.That(policy.Build(CreateSession(shown), false).SignatureChargedVisible, Is.False, "nothing charged, nothing to show");
        }

        [Test]
        public void PublicState_AChargeComesFromTheChargeModel()
        {
            RulesSettings settings = GddStartingValues.CreateRulesSettings();
            settings.Signature.OpponentSeesCharged = true;
            var policies = new RulePolicies { SignatureCharge = new AlwaysChargedModel() };

            PlanningSession session = CreateSession(settings, policies: policies);

            Assert.That(session.PublicState.SignatureChargedVisible, Is.True);
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

        private sealed class AlwaysChargedModel : XRim.Rules.Signature.ISignatureChargeModel
        {
            public bool IsAvailable(Side side) => true;

            public void OnDamageDealt(Side side, float hpDamage)
            {
            }

            public void OnDamageTaken(Side side, float hpDamage)
            {
            }

            public void OnUsed(Side side)
            {
            }
        }
    }
}
