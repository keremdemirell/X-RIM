using System;
using NUnit.Framework;
using XRim.Rules;
using XRim.Rules.Planning;
using XRim.Rules.Settings;
using XRim.Rules.Status;
using XRim.Tests.EditMode.Rules.Planning;

namespace XRim.Tests.EditMode.Rules.Status
{
    /// <summary>What a stun (and a stagger, which shares it) does to the next turn: the three GDD §11 options (D17).</summary>
    public sealed class StatusEffectTests
    {
        private const float Tolerance = 1e-4f;

        private RulesSettings _settings;
        private IStatusEffectFactory _factory;

        [SetUp]
        public void SetUp()
        {
            _settings = GddStartingValues.CreateRulesSettings();
            _factory = new RulePolicies().StatusEffects;
        }

        private PlanningConstraints Constraints() => PlanningConstraints.CreateDefault(_settings.Match);

        private PlanningConstraints StunnedConstraints()
        {
            PlanningConstraints constraints = Constraints();
            _factory.Create(StatusKind.Stunned, _settings.Damage).ApplyToNextTurn(constraints);
            return constraints;
        }

        [Test]
        public void D17Default_IsNoBodyMove_ForTheNextTurnOnly()
        {
            IStatusEffect stun = _factory.Create(StatusKind.Stunned, _settings.Damage);

            Assert.That(_settings.Damage.StunEffect, Is.EqualTo(StunEffect.NoBodyMove));
            Assert.That(stun, Is.InstanceOf<NoBodyMoveStatus>());
            Assert.That(stun.Kind, Is.EqualTo(StatusKind.Stunned));
            Assert.That(stun.RemainingTurns, Is.EqualTo(1));
            Assert.That(stun.WithOneTurnUsed(), Is.Null);
        }

        [Test]
        public void NoBodyMove_ForbidsEveryMove_ButNone_AndNothingElse()
        {
            PlanningConstraints constraints = StunnedConstraints();

            foreach (BodyMove move in (BodyMove[])Enum.GetValues(typeof(BodyMove)))
            {
                Assert.That(constraints.IsBodyMoveAllowed(move), Is.EqualTo(move == BodyMove.None), move.ToString());
            }

            Assert.That(constraints.PlanningDurationSeconds, Is.EqualTo(_settings.Match.PlanningDurationSeconds));
            Assert.That(constraints.InkLengthMultiplier, Is.EqualTo(1f));
        }

        [Test]
        public void AStunnedPlayer_CannotSetABodyMove_ButStillDrawsAndPicksWeapons()
        {
            PlanningSession session = PlanningTestData.CreateSession(_settings, StunnedConstraints());

            Assert.That(session.Apply(new SetBodyMoveCommand(BodyMove.Jump), 1.0).Rejection, Is.EqualTo(CommandRejection.BodyMoveNotAllowed));
            Assert.That(session.Apply(new SetBodyMoveCommand(BodyMove.None), 1.0).Accepted, Is.True);
            Assert.That(session.Apply(new SetPathCommand(PlanningTestData.Thrust(400f)), 1.0).Accepted, Is.True);
            Assert.That(session.Apply(new SelectWeaponCommand(WeaponIds.Mace), 2.0).Accepted, Is.True);
        }

        [Test]
        public void ShorterPlanning_ScalesThePlanningTime()
        {
            _settings.Damage.StunEffect = StunEffect.ShorterPlanning;

            PlanningConstraints constraints = StunnedConstraints();

            Assert.That(constraints.PlanningDurationSeconds,
                Is.EqualTo(_settings.Match.PlanningDurationSeconds * _settings.Damage.StunPlanningDurationFraction).Within(Tolerance));
            Assert.That(constraints.IsBodyMoveAllowed(BodyMove.Lunge), Is.True);
        }

        [Test]
        public void LessInk_ScalesTheInkBudget()
        {
            _settings.Damage.StunEffect = StunEffect.LessInk;

            PlanningConstraints constraints = StunnedConstraints();

            Assert.That(constraints.InkLengthMultiplier, Is.EqualTo(_settings.Damage.StunInkLengthFraction).Within(Tolerance));
            Assert.That(constraints.IsBodyMoveAllowed(BodyMove.Lunge), Is.True);
        }

        [Test]
        public void AStagger_SharesTheStunsDefinition()
        {
            IStatusEffect stagger = _factory.Create(StatusKind.Staggered, _settings.Damage);

            Assert.That(stagger.Kind, Is.EqualTo(StatusKind.Staggered));
            Assert.That(stagger, Is.InstanceOf<NoBodyMoveStatus>(), "§10 proposal: same as head stun");
        }

        [Test]
        public void EffectsCountDown_WithoutChangingThemselves()
        {
            var effect = new LessInkStatus(StatusKind.Stunned, 2, 0.5f);

            IStatusEffect next = effect.WithOneTurnUsed();

            Assert.That(effect.RemainingTurns, Is.EqualTo(2), "immutable: cloned match states may share it");
            Assert.That(next.RemainingTurns, Is.EqualTo(1));
            Assert.That(((LessInkStatus)next).InkLengthFraction, Is.EqualTo(0.5f));
            Assert.That(next.WithOneTurnUsed(), Is.Null);
        }

        [Test]
        public void Validation_KeepsTheStunValuesWorkable()
        {
            var issues = new System.Collections.Generic.List<string>();
            _settings.Validate(issues);
            Assert.That(issues, Is.Empty, "the starting values are valid");

            _settings.Damage.StunInkLengthFraction = 0f;
            _settings.Damage.StunPlanningDurationFraction = 0.1f; // 1.2 s, inside the 1.5 s lock-out
            _settings.Validate(issues);
            Assert.That(issues, Has.Some.Contains("ink fraction"));
            Assert.That(issues, Has.Some.Contains("lock-out"));
        }

        [Test]
        public void Validation_RefusesALimbCapThatLetsTwoHitsSever()
        {
            var issues = new System.Collections.Generic.List<string>();
            _settings.Damage.PerHitLimbCapFraction = 0.5f;

            _settings.Validate(issues);

            Assert.That(issues, Has.Some.Contains("at least 3 hits"));
        }
    }
}
