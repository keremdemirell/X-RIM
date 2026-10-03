using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using XRim.Core;
using XRim.Rules;
using XRim.Rules.Arena;
using XRim.Rules.Combat;
using XRim.Rules.Events;
using XRim.Rules.Match;
using XRim.Rules.Paths;
using XRim.Rules.Planning;
using XRim.Rules.Settings;
using XRim.Rules.Status;
using XRim.Simulation;
using XRim.Simulation.Execution;
using XRim.Simulation.Physics;
using XRim.Simulation.Recording;

namespace XRim.Tests.EditMode.Simulation.Execution
{
    /// <summary>
    /// Clashes and shield blocks inside the turn loop, on the fake world (Session 07; GDD §7, §10). Both dummies are right-handed;
    /// the left pelvis is at x = 0, the right at x = 600. The left weapon thrusts along y = 100 from (400, 100) to (640, 100); a
    /// right weapon with a path swings high and away (y = 300), so it is travelling but touches nothing; a right shield without a
    /// path is held still, centred at (600, 100) with its face toward the left (its front at x = 580).
    /// </summary>
    public sealed class ClashAndBlockHandlingTests
    {
        private const float ShoulderHeight = 100f;
        private const float RightX = 600f;
        private const float ShieldFrontX = 580f;
        private static readonly WeaponPath LeftThrust = new WeaponPath(new[] { new Vec2(400f, ShoulderHeight), new Vec2(640f, ShoulderHeight) });
        private static readonly WeaponPath RightHighSwing = new WeaponPath(new[] { new Vec2(300f, 300f), new Vec2(100f, 300f) });
        private static readonly Vec2 SquareNormal = new Vec2(1f, 0f);
        private static readonly Vec2 GlancingNormal = new Vec2(0f, 1f);

        private RulesSettings _rules;
        private SimulationSettings _simulation;
        private FakePhysicsWorld _world;
        private SimClock _clock;
        private PoseSnapshot _pose;
        private MatchState _state;

        [SetUp]
        public void SetUp()
        {
            _rules = GddStartingValues.CreateRulesSettings();
            _simulation = new SimulationSettings();
            _world = new FakePhysicsWorld();
            _clock = new SimClock(_simulation.StepRateHz);
            _pose = new PoseSnapshot();
            _pose.Left.Set(BodyPart.Torso, new BodyPose(Vec2.Zero, 0f));
            _pose.Right.Set(BodyPart.Torso, new BodyPose(new Vec2(RightX, 0f), 0f));
            _pose.Left.HeldItem = new BodyPose(new Vec2(0f, ShoulderHeight), 0f);
            _pose.Right.HeldItem = new BodyPose(new Vec2(RightX, ShoulderHeight), 180f);
            Arm(WeaponIds.Rapier, WeaponIds.Rapier);
        }

        [TearDown]
        public void TearDown() => _world.Dispose();

        private void Arm(WeaponId left, WeaponId right) => _state = new MatchState(
            new PerSide<FighterState>(new FighterState(Handedness.Right, _rules.Damage.MaxHp, left),
                new FighterState(Handedness.Right, _rules.Damage.MaxHp, right)),
            PerSide<ElectricWallState>.Create(_ => new ElectricWallState()));

        private WeaponStats Held(Side side) => _rules.FindWeapon(_state.Fighters[side].CurrentWeapon);

        private TurnPlan Plan(Side side, WeaponPath path) =>
            new TurnPlan(_state.Fighters[side].CurrentWeapon, BodyMove.None, path ?? WeaponPath.Empty, default, true);

        /// <summary>The left thrusts; the right swings high (or, holding a shield, holds it still).</summary>
        private TurnResult Run(WeaponPath leftPath = null, WeaponPath rightPath = null)
        {
            leftPath = leftPath ?? LeftThrust;
            if (rightPath == null && Held(Side.Right).Kind != WeaponKind.Shield) rightPath = RightHighSwing;
            var plans = new PerSide<TurnPlan>(Plan(Side.Left, leftPath), Plan(Side.Right, rightPath));
            return new TurnSimulator(_world, new RulePolicies()).Simulate(new TurnInput(new BoardSnapshot(_state, _pose), plans, _rules, _simulation));
        }

        private float LeftTipX(double seconds) => 400f + (float)(Held(Side.Left).SpeedUnitsPerSecond * seconds);

        /// <summary>Where the left blade touches at that time: half its width ahead of its tip.</summary>
        private Vec2 LeftTouchPoint(double seconds) => new Vec2(LeftTipX(seconds) + Held(Side.Left).InkThicknessUnits * 0.5f, ShoulderHeight);

        /// <summary>When the left blade's front reaches the shield's face.</summary>
        private double ShieldReachedAt() =>
            (ShieldFrontX - Held(Side.Left).InkThicknessUnits * 0.5f - 400f) / Held(Side.Left).SpeedUnitsPerSecond;

        private static BodyTag Item(Side side) => new BodyTag(side, BodyRole.HeldItem, BodyPart.Torso);

        /// <summary>The two held items touch at a time (the engine reports it a step later): normal from the left item to the right.</summary>
        private void ScheduleItemContact(double seconds, Vec2 normal, Vec2 relativeVelocity, Vec2? point = null) =>
            _world.ScheduleContacts(_clock.StepsFor(seconds) + 1,
                new ContactFacts(Item(Side.Left), Item(Side.Right), point ?? LeftTouchPoint(seconds), normal, relativeVelocity));

        /// <summary>A side's weapon touches a body part of the other dummy at a time.</summary>
        private void ScheduleHit(Side attacker, BodyPart part, double seconds, Vec2? point = null) =>
            _world.ScheduleContacts(_clock.StepsFor(seconds) + 1,
                new ContactFacts(Item(attacker), new BodyTag(attacker.Opponent(), BodyRole.BodyPart, part), point ?? LeftTouchPoint(seconds),
                    new Vec2(attacker.FacingSign(), 0f), new Vec2(-900f * attacker.FacingSign(), 0f)));

        private static List<T> EventsOf<T>(TurnResult result) where T : MatchEvent => result.Timeline.Events.OfType<T>().ToList();

        private float HpLost(TurnResult result, Side side) => _rules.Damage.MaxHp - result.Report.ResolvedState.Fighters[side].Hp;

        private List<(Side Side, Vec2 Velocity, float AngularVelocity)> ReleasesOf(Side side) =>
            _world.Releases.Where(release => release.Side == side).ToList();

        private Vec2 Tip(Side side, BodyPose grip) => HeldItemShape.Of(Held(side)).PathPointAt(grip);

        private float LastRootX(Side side) => _world.RootTargets.Last(target => target.Side == side).Pose.PositionUnits.X;

        // --- Clashes (§10) --------------------------------------------------------------------

        [Test]
        public void AMaceCrushingARapier_SwatsTheRapierBack_StaggersItsDummy_AndTheMaceCarriesOn()
        {
            Arm(WeaponIds.Rapier, WeaponIds.Mace);
            ScheduleItemContact(0.05, SquareNormal, new Vec2(-900f, 0f));

            TurnResult result = Run();

            WeaponClashEvent clash = EventsOf<WeaponClashEvent>(result).Single();
            Assert.That(clash.Result.Kind, Is.EqualTo(ClashKind.CrushThrough));
            Assert.That(clash.Result.Winner, Is.EqualTo(Side.Right));
            Assert.That(EventsOf<ContactEvent>(result), Has.Count.EqualTo(1), "the raw contact stays in the debug list");

            float knock = 10f * 250f / 2f * _simulation.ClashReaction.KnockOffMomentumFraction;
            Vec2 released = ReleasesOf(Side.Left).Single().Velocity;
            Assert.That(released.X, Is.EqualTo(-knock).Within(1f), "kicked away from the mace, its own forward motion stopped");
            Assert.That(ReleasesOf(Side.Right), Is.Empty, "the mace stays on its path");

            Assert.That(result.Report.ResolvedState.Fighters[Side.Left].Statuses.Single().Kind, Is.EqualTo(StatusKind.Staggered));
            Assert.That(EventsOf<StatusAppliedEvent>(result).Single().Side, Is.EqualTo(Side.Left));
            Assert.That(Tip(Side.Left, result.FinalBoard.Pose.Left.HeldItem).X, Is.LessThan(LeftTipX(0.06)), "knocked off its path");
            Vec2 maceEnd = new Vec2(RightX - 100f, 300f);
            Assert.That(Vec2.Distance(Tip(Side.Right, result.FinalBoard.Pose.Right.HeldItem), maceEnd), Is.LessThan(0.5f),
                "the mace runs its whole path");
        }

        [Test]
        public void TheCrushThroughWinner_HitsSofterAfterwards()
        {
            Arm(WeaponIds.Rapier, WeaponIds.Mace);
            ScheduleItemContact(0.05, SquareNormal, new Vec2(-900f, 0f));
            ScheduleHit(Side.Right, BodyPart.Torso, 0.4);

            TurnResult result = Run();

            Assert.That(HpLost(result, Side.Left), Is.EqualTo(20f * _rules.Clash.CrushThroughDamageMultiplier).Within(1e-4f), "20 × 0.7");
        }

        [Test]
        public void TwoRapiersMeetingSquareOn_BothReboundBack_AndNeitherCarriesOn()
        {
            ScheduleItemContact(0.05, SquareNormal, new Vec2(-1800f, 0f));
            ScheduleHit(Side.Left, BodyPart.Torso, 0.15);

            TurnResult result = Run();

            Assert.That(EventsOf<WeaponClashEvent>(result).Single().Result.Kind, Is.EqualTo(ClashKind.BothRebound));
            Assert.That(ReleasesOf(Side.Left).Single().Velocity.X,
                Is.EqualTo(-900f * _simulation.ClashReaction.ReboundSpeedFraction).Within(1f), "bounced back");
            Assert.That(ReleasesOf(Side.Right), Has.Count.EqualTo(1));
            Assert.That(EventsOf<HitLandedEvent>(result), Is.Empty, "a rebounded weapon lands nothing");
            Assert.That(EventsOf<StatusAppliedEvent>(result), Is.Empty);
        }

        [Test]
        public void AGlancingRapier_DeflectsTheMace_WhichFliesOffSideways_AndTheRapierCarriesOn()
        {
            Arm(WeaponIds.Rapier, WeaponIds.Mace);
            ScheduleItemContact(0.05, GlancingNormal, new Vec2(-900f, 0f));

            TurnResult result = Run();

            WeaponClashEvent clash = EventsOf<WeaponClashEvent>(result).Single();
            Assert.That(clash.Result.Kind, Is.EqualTo(ClashKind.LighterDeflectsHeavier));
            Assert.That(clash.Result.Winner, Is.EqualTo(Side.Left));
            float sideways = 2f * 900f / 10f * _simulation.ClashReaction.KnockOffMomentumFraction;
            Assert.That(ReleasesOf(Side.Right).Single().Velocity.Y, Is.EqualTo(sideways).Within(1e-2f), "pushed up, away from the rapier");
            Assert.That(ReleasesOf(Side.Left), Is.Empty);
            Assert.That(EventsOf<StatusAppliedEvent>(result), Is.Empty, "a deflection staggers nobody");
            Assert.That(Tip(Side.Left, result.FinalBoard.Pose.Left.HeldItem).X, Is.EqualTo(640f).Within(0.5f), "the rapier runs its path");
        }

        [Test]
        public void TwoRapiersGlancing_SlidePast_AndBothCarryOn()
        {
            ScheduleItemContact(0.05, GlancingNormal, new Vec2(-1800f, 0f));

            TurnResult result = Run();

            Assert.That(EventsOf<WeaponClashEvent>(result).Single().Result.Kind, Is.EqualTo(ClashKind.BothSlidePast));
            Assert.That(_world.Releases, Is.Empty);
            Assert.That(Tip(Side.Left, result.FinalBoard.Pose.Left.HeldItem).X, Is.EqualTo(640f).Within(0.5f));
        }

        [Test]
        public void D19_ALaterContactBetweenTheSameItems_PassesThrough()
        {
            ScheduleItemContact(0.05, GlancingNormal, new Vec2(-1800f, 0f));
            ScheduleItemContact(0.1, SquareNormal, new Vec2(-1800f, 0f));

            TurnResult result = Run();

            Assert.That(EventsOf<WeaponClashEvent>(result), Has.Count.EqualTo(1));
            Assert.That(EventsOf<ContactEvent>(result), Has.Count.EqualTo(2), "both raw contacts are listed");
            Assert.That(_world.Releases, Is.Empty, "the second contact would have rebounded both");
        }

        [Test]
        public void Priority_AHitBeforeTheBladesMeet_LandsFirst_AndTheInterruptedBladeClashesHeldStill()
        {
            ScheduleHit(Side.Left, BodyPart.Head, 0.02);
            ScheduleItemContact(0.1, SquareNormal, new Vec2(-900f, 0f));

            TurnResult result = Run();

            HitLandedEvent hit = EventsOf<HitLandedEvent>(result).Single();
            WeaponClashEvent clash = EventsOf<WeaponClashEvent>(result).Single();
            Assert.That(hit.Time, Is.LessThan(clash.Time));
            Assert.That(clash.Result.RightPower, Is.EqualTo(_rules.FindWeapon(WeaponIds.Rapier).Mass).Within(1e-4f), "A1: interrupted, its mass alone");
            Assert.That(clash.Result.Winner, Is.EqualTo(Side.Left));
        }

        // --- Shield blocks (§7) ----------------------------------------------------------------

        [Test]
        public void AFullBlock_BouncesTheThrustBack_PushesTheHolderBack_AndTheThrustLandsNothing()
        {
            Arm(WeaponIds.Rapier, WeaponIds.Shield);
            double reached = ShieldReachedAt();
            ScheduleItemContact(reached, SquareNormal, new Vec2(-900f, 0f));
            ScheduleHit(Side.Left, BodyPart.Torso, reached + 0.03);

            TurnResult result = Run();

            ShieldBlockEvent block = EventsOf<ShieldBlockEvent>(result).Single();
            Assert.That(block.Result.AttackStopped, Is.True);
            Assert.That(block.Blocker, Is.EqualTo(Side.Right));
            Assert.That(block.ContactAngleDegrees, Is.EqualTo(90f).Within(1e-3f));
            Assert.That(block.FacePositionFraction, Is.EqualTo(0f).Within(1e-3f));
            Assert.That(block.Time.Seconds, Is.EqualTo(reached).Within(2e-5), "the moving weapon's arrival, not the still shield's");
            Assert.That(ReleasesOf(Side.Left).Single().Velocity.X, Is.EqualTo(-450f).Within(1f), "bounced back off the shield");
            Assert.That(EventsOf<HitLandedEvent>(result), Is.Empty);
            Assert.That(HpLost(result, Side.Right), Is.EqualTo(0f));

            float push = _simulation.ClashReaction.BlockKnockbackFraction;
            Assert.That(LastRootX(Side.Right), Is.EqualTo(RightX + 2f * _simulation.HitReaction.KnockbackUnitsPerWeaponMass * push).Within(1e-3f),
                "D21: the holder is pushed back");
            (Side _, BodyPart part, Vec2 impulse) = _world.Impulses.Single();
            Assert.That(part, Is.EqualTo(BodyPart.Torso));
            Assert.That(impulse.X, Is.EqualTo(2f * 900f * _simulation.HitReaction.PartImpulseMomentumFraction * push).Within(1e-2f));
            Assert.That(EventsOf<BodyMoveStartedEvent>(result), Is.Empty, "§13: pushed back behind a shield is no retreat");
        }

        [Test]
        public void ARimHit_IsAPartialBlock_TheThrustCarriesOn_AndItsHitDealsHalf()
        {
            Arm(WeaponIds.Rapier, WeaponIds.Shield);
            double reached = ShieldReachedAt();
            ScheduleItemContact(reached, SquareNormal, new Vec2(-900f, 0f), new Vec2(ShieldFrontX, ShoulderHeight + 70f));
            ScheduleHit(Side.Left, BodyPart.Torso, reached + 0.03);

            TurnResult result = Run();

            ShieldBlockEvent block = EventsOf<ShieldBlockEvent>(result).Single();
            Assert.That(block.FacePositionFraction, Is.EqualTo(70f / 75f).Within(1e-3f));
            Assert.That(block.Result.AttackStopped, Is.False);
            Assert.That(ReleasesOf(Side.Left), Is.Empty, "the thrust carries on through the rim");
            Assert.That(HpLost(result, Side.Right), Is.EqualTo(8f * 0.5f).Within(1e-4f), "A5: its later hit deals half");
        }

        [Test]
        public void AGlancingHitOnTheFace_IsAPartialBlock()
        {
            Arm(WeaponIds.Rapier, WeaponIds.Shield);
            ScheduleItemContact(ShieldReachedAt(), SquareNormal, new Vec2(0f, -900f));

            TurnResult result = Run();

            ShieldBlockEvent block = EventsOf<ShieldBlockEvent>(result).Single();
            Assert.That(block.ContactAngleDegrees, Is.EqualTo(0f).Within(1e-3f), "sliding along the face");
            Assert.That(block.Result.AttackStopped, Is.False);
        }

        [Test]
        public void A2_AtTheSameInstant_TheShieldInTheWayBeatsTheBodyHit()
        {
            Arm(WeaponIds.Rapier, WeaponIds.Shield);
            double reached = ShieldReachedAt();
            Vec2 point = LeftTouchPoint(reached);
            ScheduleItemContact(reached, SquareNormal, new Vec2(-900f, 0f), point);
            ScheduleHit(Side.Left, BodyPart.Torso, reached, point);

            TurnResult result = Run();

            Assert.That(EventsOf<ContactEvent>(result).Select(contact => contact.Time).Distinct().Count(), Is.EqualTo(1), "the same instant");
            Assert.That(EventsOf<ShieldBlockEvent>(result).Single().Result.AttackStopped, Is.True);
            Assert.That(EventsOf<HitLandedEvent>(result), Is.Empty);
        }

        [Test]
        public void D21StaggersHolder_AMaceFullyBlocked_StaggersTheShieldHolder()
        {
            _rules.Clash.ShieldBlock.HeavyWeapon = HeavyWeaponBlockRule.StaggersHolder;
            Arm(WeaponIds.Mace, WeaponIds.Shield);
            ScheduleItemContact(ShieldReachedAt(), SquareNormal, new Vec2(-250f, 0f));

            TurnResult result = Run();

            Assert.That(EventsOf<ShieldBlockEvent>(result).Single().Result.ShieldHolderStaggered, Is.True);
            Assert.That(result.Report.ResolvedState.Fighters[Side.Right].Statuses.Single().Kind, Is.EqualTo(StatusKind.Staggered));
        }

        [Test]
        public void AWeaponHeldStill_IsNotBlocked()
        {
            Arm(WeaponIds.Rapier, WeaponIds.Shield);
            ScheduleItemContact(0.05, SquareNormal, new Vec2(-900f, 0f), new Vec2(ShieldFrontX, ShoulderHeight));

            TurnResult result = Run(WeaponPath.Empty);

            Assert.That(EventsOf<ShieldBlockEvent>(result), Is.Empty);
            Assert.That(_world.Releases, Is.Empty);
        }

        [Test]
        public void A7_TwoShieldsMeeting_HaveNoRule()
        {
            Arm(WeaponIds.Shield, WeaponIds.Shield);
            var shieldPush = new WeaponPath(new[] { new Vec2(150f, ShoulderHeight), new Vec2(240f, ShoulderHeight) });
            ScheduleItemContact(0.1, SquareNormal, new Vec2(-400f, 0f), new Vec2(300f, ShoulderHeight));

            TurnResult result = Run(shieldPush);

            Assert.That(EventsOf<ShieldBlockEvent>(result), Is.Empty);
            Assert.That(EventsOf<WeaponClashEvent>(result), Is.Empty);
            Assert.That(EventsOf<ContactEvent>(result), Has.Count.EqualTo(1));
        }
    }
}
