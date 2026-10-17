using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using XRim.Core;
using XRim.Rules;
using XRim.Rules.Arena;
using XRim.Rules.Events;
using XRim.Rules.Match;
using XRim.Rules.Paths;
using XRim.Rules.Planning;
using XRim.Rules.Settings;
using XRim.Simulation;
using XRim.Simulation.Execution;
using XRim.Simulation.Physics;
using XRim.Simulation.Recording;

namespace XRim.Tests.EditMode.Simulation.Execution
{
    /// <summary>
    /// Signature moves inside the turn loop, on the fake world (GDD §8): the move's special property rides on its side's attack (the
    /// knockback burst pushes the victim farther, the stun and damage boost act on its hits) and the move is revealed at the first
    /// instant. Both dummies hold rapiers; the left pelvis is at x = 0, the right at x = 600 (the hit harness of
    /// <see cref="HitContactHandlerTests"/>).
    /// </summary>
    public sealed class SignatureEffectSimulationTests
    {
        private const float ShoulderHeight = 100f;
        private const float RightX = 600f;
        private const double HitSeconds = 0.0505;
        private static readonly WeaponPath StraightThrust =
            new WeaponPath(new[] { new Vec2(400f, ShoulderHeight), new Vec2(640f, ShoulderHeight) });

        private RulesSettings _rules;
        private SimulationSettings _simulation;
        private FakePhysicsWorld _world;
        private SimClock _clock;
        private PoseSnapshot _pose;
        private MatchState _state;
        private WeaponStats _rapier;

        [SetUp]
        public void SetUp()
        {
            _rules = GddStartingValues.CreateRulesSettings();
            _simulation = new SimulationSettings();
            _world = new FakePhysicsWorld();
            _clock = new SimClock(_simulation.StepRateHz);
            _rapier = _rules.FindWeapon(WeaponIds.Rapier);
            _state = new MatchState(
                PerSide<FighterState>.Create(_ => new FighterState(Handedness.Right, _rules.Damage.MaxHp, WeaponIds.Rapier)),
                PerSide<ElectricWallState>.Create(_ => new ElectricWallState()));
            _pose = new PoseSnapshot();
            _pose.Left.Set(BodyPart.Torso, new BodyPose(Vec2.Zero, 0f));
            _pose.Right.Set(BodyPart.Torso, new BodyPose(new Vec2(RightX, 0f), 0f));
            _pose.Left.HeldItem = new BodyPose(new Vec2(0f, ShoulderHeight), 0f);
            _pose.Right.HeldItem = new BodyPose(new Vec2(RightX, ShoulderHeight), 180f);
        }

        [TearDown]
        public void TearDown() => _world.Dispose();

        /// <summary>A plan whose path is a straight thrust, holding a signature move or not (the simulator plays the path either way).</summary>
        private static TurnPlan Plan(SignatureMoveId signature = default, WeaponPath path = null) =>
            new TurnPlan(WeaponIds.Rapier, BodyMove.None, path ?? StraightThrust, signature, true);

        private TurnResult Run(TurnPlan left, TurnPlan right = null) =>
            new TurnSimulator(_world, new RulePolicies()).Simulate(new TurnInput(new BoardSnapshot(_state, _pose),
                new PerSide<TurnPlan>(left, right ?? Plan(path: WeaponPath.Empty)), _rules, _simulation));

        private void ScheduleHit(Side attacker, BodyPart part)
        {
            float sign = attacker.FacingSign();
            float tipX = (float)(_rapier.SpeedUnitsPerSecond * HitSeconds);
            float x = attacker == Side.Left ? 400f + tipX : RightX - 400f - tipX;
            var facts = new ContactFacts(new BodyTag(attacker, BodyRole.HeldItem, BodyPart.Torso),
                new BodyTag(attacker.Opponent(), BodyRole.BodyPart, part),
                new Vec2(x + sign * _rapier.InkThicknessUnits * 0.5f, ShoulderHeight), new Vec2(sign, 0f),
                new Vec2(-_rapier.SpeedUnitsPerSecond * sign, 0f));
            _world.ScheduleContacts(_clock.StepsFor(HitSeconds) + 1, facts);
        }

        private float LastRootX(Side side) => _world.RootTargets.Last(target => target.Side == side).Pose.PositionUnits.X;

        private float NormalKnock => _rapier.Mass * _simulation.HitReaction.KnockbackUnitsPerWeaponMass;

        private float HpLost(TurnResult result, Side side) => _rules.Damage.MaxHp - result.Report.ResolvedState.Fighters[side].Hp;

        private static List<T> EventsOf<T>(TurnResult result) where T : MatchEvent => result.Timeline.Events.OfType<T>().ToList();

        // --- 5 Finger Exploding Touch: knockback burst ------------------------------------------------

        [Test]
        public void TheKnockbackBurst_ThrowsTheVictimThreeTimesAsFar()
        {
            ScheduleHit(Side.Left, BodyPart.Torso);

            Run(Plan(SignatureMoveIds.FiveFingerExplodingTouch));

            Assert.That(LastRootX(Side.Right), Is.EqualTo(RightX + 3f * NormalKnock).Within(1e-3f));
            Assert.That(LastRootX(Side.Left), Is.EqualTo(0f).Within(1e-3f), "the attacker stays");
        }

        [Test]
        public void TheSameThrust_DrawnByHand_KnocksAsEver()
        {
            ScheduleHit(Side.Left, BodyPart.Torso);

            Run(Plan());

            Assert.That(LastRootX(Side.Right), Is.EqualTo(RightX + NormalKnock).Within(1e-3f));
        }

        [Test]
        public void TheKnockbackBurst_LeavesTheOpponentsHitsAlone()
        {
            ScheduleHit(Side.Right, BodyPart.Torso);

            Run(Plan(SignatureMoveIds.FiveFingerExplodingTouch, WeaponPath.Empty), Plan());

            Assert.That(LastRootX(Side.Left), Is.EqualTo(-NormalKnock).Within(1e-3f), "the right dummy's own drawn thrust");
        }

        // --- Head Impact Simulation #404 and Whiplash Calibration Spin ----------------------------------

        [Test]
        public void TheStunOnHit_StunsTheVictim_FromATorsoHit()
        {
            ScheduleHit(Side.Left, BodyPart.Torso);

            TurnResult result = Run(Plan(SignatureMoveIds.HeadImpactSimulation404));

            Assert.That(EventsOf<HitLandedEvent>(result).Single().Damage.Stuns, Is.True);
            Assert.That(result.Report.ResolvedState.Fighters[Side.Right].Statuses, Has.Count.EqualTo(1));
        }

        [Test]
        public void TheDamageBoost_HitsHarder()
        {
            ScheduleHit(Side.Left, BodyPart.Torso);

            TurnResult result = Run(Plan(SignatureMoveIds.WhiplashCalibrationSpin));

            Assert.That(HpLost(result, Side.Right), Is.EqualTo(_rapier.BaseDamage * _rules.Signature.DamageBoostMultiplier).Within(1e-4f));
        }

        // --- Revealed at execution (§8) --------------------------------------------------------------------

        [Test]
        public void TheMove_IsRevealedAtTheFirstInstant_WithItsName()
        {
            TurnResult result = Run(Plan(SignatureMoveIds.HeadImpactSimulation404), Plan());

            SignatureUsedEvent used = EventsOf<SignatureUsedEvent>(result).Single();
            Assert.That(used.Time, Is.EqualTo(SimTime.Zero));
            Assert.That(used.Side, Is.EqualTo(Side.Left));
            Assert.That(used.Move, Is.EqualTo(SignatureMoveIds.HeadImpactSimulation404));
            Assert.That(used.DisplayName, Is.EqualTo("Head Impact Simulation #404"));
        }

        [Test]
        public void ADrawnPath_RevealsNoMove()
        {
            Assert.That(EventsOf<SignatureUsedEvent>(Run(Plan(), Plan())), Is.Empty);
        }

        [Test]
        public void AMoveTheCatalogueDoesNotKnow_IsNamedByItsId_AndDoesNothingSpecial()
        {
            ScheduleHit(Side.Left, BodyPart.Torso);

            TurnResult result = Run(Plan(new SignatureMoveId("ghost_move")));

            Assert.That(EventsOf<SignatureUsedEvent>(result).Single().DisplayName, Is.EqualTo("ghost_move"));
            Assert.That(LastRootX(Side.Right), Is.EqualTo(RightX + NormalKnock).Within(1e-3f));
            Assert.That(HpLost(result, Side.Right), Is.EqualTo(_rapier.BaseDamage).Within(1e-4f));
        }
    }
}
