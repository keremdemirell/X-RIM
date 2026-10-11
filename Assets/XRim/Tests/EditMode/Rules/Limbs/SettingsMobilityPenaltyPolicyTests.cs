using NUnit.Framework;
using XRim.Rules;
using XRim.Rules.Limbs;
using XRim.Rules.Match;
using XRim.Rules.Planning;
using XRim.Rules.Settings;

namespace XRim.Tests.EditMode.Rules.Limbs
{
    /// <summary>Leg loss (GDD §12, TBD). Default (designer, 2026-10-10): no jump or lunge on one leg, no body moves on none.</summary>
    public sealed class SettingsMobilityPenaltyPolicyTests
    {
        private static readonly BodyMove[] EveryMove = { BodyMove.None, BodyMove.Crouch, BodyMove.Lunge, BodyMove.StepBack, BodyMove.Jump };

        private readonly SettingsMobilityPenaltyPolicy _policy = new SettingsMobilityPenaltyPolicy();
        private RulesSettings _settings;
        private FighterState _fighter;

        [SetUp]
        public void SetUp()
        {
            _settings = GddStartingValues.CreateRulesSettings();
            _fighter = new FighterState(Handedness.Right, _settings.Damage.MaxHp, WeaponIds.Rapier);
        }

        private PlanningConstraints Constraints()
        {
            PlanningConstraints constraints = PlanningConstraints.CreateDefault(_settings.Match);
            _policy.Apply(_fighter, constraints, _settings);
            return constraints;
        }

        [Test]
        public void TheDefault_IsNoJumpOrLungeOnOneLeg_ThroughThisPolicy()
        {
            Assert.That(new LimbLossSettings().LegLossPenalty, Is.EqualTo(LegLossPenalty.NoJumpOrLungeOnOneLeg));
            Assert.That(new RulePolicies().MobilityPenalty, Is.InstanceOf<SettingsMobilityPenaltyPolicy>());
        }

        [Test]
        public void BothLegs_ForbidNothing()
        {
            PlanningConstraints constraints = Constraints();

            foreach (BodyMove move in EveryMove) Assert.That(constraints.IsBodyMoveAllowed(move), Is.True, move.ToString());
        }

        [TestCase(BodyPart.LeftLeg)]
        [TestCase(BodyPart.RightLeg)]
        public void OneLeg_ForbidsJumpAndLunge_Only(BodyPart lost)
        {
            _fighter.MarkSevered(lost);

            PlanningConstraints constraints = Constraints();

            Assert.That(constraints.IsBodyMoveAllowed(BodyMove.Jump), Is.False);
            Assert.That(constraints.IsBodyMoveAllowed(BodyMove.Lunge), Is.False);
            Assert.That(constraints.IsBodyMoveAllowed(BodyMove.Crouch), Is.True);
            Assert.That(constraints.IsBodyMoveAllowed(BodyMove.StepBack), Is.True);
            Assert.That(constraints.IsBodyMoveAllowed(BodyMove.None), Is.True);
        }

        [Test]
        public void NoLegs_ForbidEveryBodyMove()
        {
            _fighter.MarkSevered(BodyPart.LeftLeg);
            _fighter.MarkSevered(BodyPart.RightLeg);

            PlanningConstraints constraints = Constraints();

            foreach (BodyMove move in EveryMove)
            {
                Assert.That(constraints.IsBodyMoveAllowed(move), Is.EqualTo(move == BodyMove.None), move.ToString());
            }
        }

        [Test]
        public void LosingArms_ChangesNoBodyMove()
        {
            _fighter.MarkSevered(BodyPart.LeftArm);
            _fighter.MarkSevered(BodyPart.RightArm);

            PlanningConstraints constraints = Constraints();

            foreach (BodyMove move in EveryMove) Assert.That(constraints.IsBodyMoveAllowed(move), Is.True, move.ToString());
        }

        [Test]
        public void TheNoPenaltyOption_ForbidsNothing_EvenWithoutLegs()
        {
            _settings.Damage.LimbLoss.LegLossPenalty = LegLossPenalty.NoPenalty;
            _fighter.MarkSevered(BodyPart.LeftLeg);
            _fighter.MarkSevered(BodyPart.RightLeg);

            PlanningConstraints constraints = Constraints();

            foreach (BodyMove move in EveryMove) Assert.That(constraints.IsBodyMoveAllowed(move), Is.True, move.ToString());
        }
    }
}
