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
using XRim.Rules.Status;
using XRim.Simulation;
using XRim.Simulation.Execution;
using XRim.Simulation.Physics;
using XRim.Simulation.Recording;

namespace XRim.Tests.EditMode.Simulation.Execution
{
    /// <summary>
    /// The hit rules inside the turn loop, on the fake world (Session 06): contacts become hits, and the simulation applies
    /// what the rules decide. Both right-handed dummies hold rapiers; the left pelvis is at x = 0, the right at x = 600. A
    /// thrust along y = 100 has the left tip at (400 + v·t, 100) and the right tip at (200 − v·t, 100).
    /// </summary>
    public sealed class HitContactHandlerTests
    {
        private const float ShoulderHeight = 100f;
        private const float RightX = 600f;
        private const float AlmostDeadHp = 5f;
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

        private static TurnPlan Plan(WeaponPath path) => new TurnPlan(WeaponIds.Rapier, BodyMove.None, path ?? WeaponPath.Empty, default, true);

        private TurnResult Run(WeaponPath leftPath, WeaponPath rightPath = null) =>
            new TurnSimulator(_world, new RulePolicies()).Simulate(new TurnInput(new BoardSnapshot(_state, _pose),
                new PerSide<TurnPlan>(Plan(leftPath), Plan(rightPath)), _rules, _simulation));

        private float TipX(Side side, double seconds)
        {
            float distance = (float)(_rapier.SpeedUnitsPerSecond * seconds);
            return side == Side.Left ? 400f + distance : RightX - 400f - distance;
        }

        /// <summary>The point the attacker's blade touches at that time: half its width ahead of the tip.</summary>
        private Vec2 TouchPoint(Side attacker, double seconds) =>
            new Vec2(TipX(attacker, seconds) + attacker.FacingSign() * _rapier.InkThicknessUnits * 0.5f, ShoulderHeight);

        /// <summary>The attacker's weapon touches a body part of the other dummy at a time; the engine reports it a step later.</summary>
        private void ScheduleHit(Side attacker, BodyPart part, double seconds)
        {
            float sign = attacker.FacingSign();
            var facts = new ContactFacts(new BodyTag(attacker, BodyRole.HeldItem, BodyPart.Torso), new BodyTag(attacker.Opponent(), BodyRole.BodyPart, part),
                TouchPoint(attacker, seconds), new Vec2(sign, 0f), new Vec2(-_rapier.SpeedUnitsPerSecond * sign, 0f));
            _world.ScheduleContacts(_clock.StepsFor(seconds) + 1, facts);
        }

        private static List<T> EventsOf<T>(TurnResult result) where T : MatchEvent => result.Timeline.Events.OfType<T>().ToList();

        private float HpLost(TurnResult result, Side side) => _rules.Damage.MaxHp - result.Report.ResolvedState.Fighters[side].Hp;

        private float LastRootX(Side side) => _world.RootTargets.Last(target => target.Side == side).Pose.PositionUnits.X;

        // --- Contacts become hits --------------------------------------------------------------

        [Test]
        public void AWeaponTouchingTheOtherDummy_LandsAHit_AtItsRefinedTime()
        {
            ScheduleHit(Side.Left, BodyPart.Torso, 0.0505);

            TurnResult result = Run(StraightThrust);

            HitLandedEvent hit = EventsOf<HitLandedEvent>(result).Single();
            Assert.That(hit.Hit.Attacker, Is.EqualTo(Side.Left));
            Assert.That(hit.Hit.Part, Is.EqualTo(BodyPart.Torso));
            Assert.That(hit.Hit.Weapon, Is.EqualTo(WeaponIds.Rapier));
            Assert.That(hit.Time.Seconds, Is.EqualTo(0.0505).Within(2e-5), "time-to-impact from the weapon's motion (§9)");
            Assert.That(EventsOf<ContactEvent>(result), Has.Count.EqualTo(1), "the raw contact stays in the debug list");
            Assert.That(HpLost(result, Side.Right), Is.EqualTo(_rapier.BaseDamage).Within(1e-4f));
            Assert.That(result.FinalBoard.State.Fighters[Side.Right].Hp, Is.EqualTo(result.Report.ResolvedState.Fighters[Side.Right].Hp));
            Assert.That(result.Report.FirstValidHitTime.Left, Is.EqualTo(hit.Time));
            Assert.That(result.Report.FirstValidHitTime.Right, Is.Null);
        }

        [TestCase(BodyPart.Head, 2.5f)]
        [TestCase(BodyPart.LeftArm, 0.8f)]
        [TestCase(BodyPart.RightLeg, 0.7f)]
        public void TheZone_ComesFromTheTouchedPartsTag(BodyPart part, float multiplier)
        {
            ScheduleHit(Side.Left, part, 0.0505);

            Assert.That(HpLost(Run(StraightThrust), Side.Right), Is.EqualTo(_rapier.BaseDamage * multiplier).Within(1e-4f));
        }

        [Test]
        public void AWeaponTouchingItsOwnDummy_IsNoHit()
        {
            _world.ScheduleContacts(13, new ContactFacts(new BodyTag(Side.Left, BodyRole.HeldItem, BodyPart.Torso),
                new BodyTag(Side.Left, BodyRole.BodyPart, BodyPart.LeftLeg), TouchPoint(Side.Left, 0.05), Vec2.UnitX, Vec2.Zero));

            TurnResult result = Run(StraightThrust);

            Assert.That(EventsOf<HitLandedEvent>(result), Is.Empty);
            Assert.That(HpLost(result, Side.Left), Is.EqualTo(0f));
        }

        [Test]
        public void BodyBumps_DealNoDamage()
        {
            _world.ScheduleContacts(5, new ContactFacts(new BodyTag(Side.Left, BodyRole.BodyPart, BodyPart.Torso),
                new BodyTag(Side.Right, BodyRole.BodyPart, BodyPart.Head), new Vec2(300f, 100f), Vec2.UnitX, new Vec2(-500f, 0f)));

            TurnResult result = Run(StraightThrust);

            Assert.That(EventsOf<HitLandedEvent>(result), Is.Empty);
            Assert.That(HpLost(result, Side.Right), Is.EqualTo(0f), "only weapons deal damage");
        }

        [Test]
        public void E2_AWeaponPastTheEndOfItsPath_DealsNoDamage()
        {
            // The thrust ends at 240 / 900 s; at 0.4 s the blade rests at the end of its path.
            Vec2 restingPoint = new Vec2(640f + _rapier.InkThicknessUnits * 0.5f, ShoulderHeight);
            _world.ScheduleContacts(_clock.StepsFor(0.4), new ContactFacts(new BodyTag(Side.Left, BodyRole.HeldItem, BodyPart.Torso),
                new BodyTag(Side.Right, BodyRole.BodyPart, BodyPart.Torso), restingPoint, Vec2.UnitX, new Vec2(-200f, 0f)));
            _world.SettlesAtStep = int.MaxValue;

            TurnResult result = Run(StraightThrust);

            Assert.That(EventsOf<ContactEvent>(result), Has.Count.EqualTo(1));
            Assert.That(EventsOf<HitLandedEvent>(result), Is.Empty);
        }

        // --- What the simulation applies -------------------------------------------------------

        [Test]
        public void TheStruckPart_GetsAShareOfTheWeaponsMomentum_AlongItsMotion()
        {
            ScheduleHit(Side.Left, BodyPart.Head, 0.0505);

            Run(StraightThrust);

            (Side side, BodyPart part, Vec2 impulse) = _world.Impulses.Single();
            float expected = _rapier.Mass * _rapier.SpeedUnitsPerSecond * _simulation.HitReaction.PartImpulseMomentumFraction;
            Assert.That(side, Is.EqualTo(Side.Right));
            Assert.That(part, Is.EqualTo(BodyPart.Head));
            Assert.That(impulse.X, Is.EqualTo(expected).Within(0.5f), "the thrust moves +x into the right dummy");
            Assert.That(impulse.Y, Is.EqualTo(0f).Within(1e-3f));
        }

        [Test]
        public void E1_TheVictim_IsKnockedAwayFromTheAttacker_AndStaysThere()
        {
            ScheduleHit(Side.Left, BodyPart.Torso, 0.0505);

            Run(StraightThrust);

            float knock = _rapier.Mass * _simulation.HitReaction.KnockbackUnitsPerWeaponMass;
            Assert.That(LastRootX(Side.Right), Is.EqualTo(RightX + knock).Within(1e-3f));
            Assert.That(LastRootX(Side.Left), Is.EqualTo(0f).Within(1e-3f), "the attacker stays");
        }

        [Test]
        public void E1_WithKnockbackOff_TheVictimKeepsItsSpot()
        {
            _simulation.HitReaction.KnockbackPersists = false;
            ScheduleHit(Side.Left, BodyPart.Torso, 0.0505);

            Run(StraightThrust);

            Assert.That(LastRootX(Side.Right), Is.EqualTo(RightX).Within(1e-3f));
            Assert.That(_world.Impulses, Has.Count.EqualTo(1), "the hit impulse still lands");
        }

        [Test]
        public void D26_AfterAHit_TheWeaponCarriesOnAtTheSpeedItKeeps()
        {
            ScheduleHit(Side.Left, BodyPart.LeftArm, 0.0505);
            _world.SettlesAtStep = int.MaxValue;

            TurnResult result = Run(StraightThrust);

            float later = TipXInFrame(result, 0.15);
            float muchLater = TipXInFrame(result, 0.25);
            float expected = _rapier.SpeedUnitsPerSecond * _rapier.SpeedKeptAfterHitFraction * 0.1f;
            Assert.That(muchLater - later, Is.EqualTo(expected).Within(0.5f), "the rapier sticks: 30% of its speed");
            Assert.That(_world.Releases, Is.Empty, "still on its path, still driven exactly");
        }

        [Test]
        public void D26_ALastHit_HandsTheBladeToPhysics_AndItRecoils()
        {
            _rules.Damage.MaxHitsPerWeaponPerTurn = 1;
            ScheduleHit(Side.Left, BodyPart.Torso, 0.0505);
            _world.SettlesAtStep = int.MaxValue;

            TurnResult result = Run(StraightThrust);

            (Side side, Vec2 velocity, float _) = _world.Releases.Single();
            Assert.That(side, Is.EqualTo(Side.Left));
            Assert.That(velocity.X, Is.EqualTo(_rapier.SpeedUnitsPerSecond).Within(1f), "D1: it carries its own speed into the hit");
            float atHit = TipX(Side.Left, 0.0505);
            float final = Tip(result.FinalBoard.Pose.Left.HeldItem).X;
            Assert.That(final, Is.LessThan(atHit - _simulation.HitReaction.RecoilDistanceUnits * 0.5f), "pulled back along its path");
        }

        [Test]
        public void D15_AHitOnTheWeaponArm_InterruptsTheOtherAttack_AndStopsItsWeapon()
        {
            ScheduleHit(Side.Left, BodyPart.RightArm, 0.0505);
            ScheduleHit(Side.Right, BodyPart.Torso, 0.1005);

            TurnResult result = Run(StraightThrust, StraightThrust);

            Assert.That(EventsOf<AttackInterruptedEvent>(result).Single().Interrupted, Is.EqualTo(Side.Right));
            Assert.That(EventsOf<HitLandedEvent>(result).Select(hit => hit.Hit.Attacker), Is.EqualTo(new[] { Side.Left }));
            Assert.That(_world.Releases.Select(release => release.Side), Does.Contain(Side.Right), "the cancelled blade is handed to physics");
            Assert.That(HpLost(result, Side.Left), Is.EqualTo(0f));
        }

        [Test]
        public void D15_AHitAwayFromTheWeaponArmAndHead_LetsBothLand()
        {
            ScheduleHit(Side.Left, BodyPart.Torso, 0.0505);
            ScheduleHit(Side.Right, BodyPart.Torso, 0.1005);

            TurnResult result = Run(StraightThrust, StraightThrust);

            Assert.That(EventsOf<HitLandedEvent>(result), Has.Count.EqualTo(2), "a trade");
            Assert.That(HpLost(result, Side.Left), Is.GreaterThan(0f));
            Assert.That(HpLost(result, Side.Right), Is.GreaterThan(0f));
            Assert.That(result.Report.FirstHitsOnSameStep, Is.False);
        }

        [Test]
        public void TwoFirstHitsInOneStep_AreReportedForSuddenDeath()
        {
            // 0.0470 s and 0.0490 s both fall in step 12 (45.8 ms to 50.0 ms at 240 Hz).
            ScheduleHit(Side.Left, BodyPart.Torso, 0.0470);
            ScheduleHit(Side.Right, BodyPart.Torso, 0.0490);

            TurnResult result = Run(StraightThrust, StraightThrust);

            Assert.That(result.Report.FirstValidHitTime.Left.HasValue && result.Report.FirstValidHitTime.Right.HasValue, Is.True);
            Assert.That(result.Report.FirstValidHitTime.Left.Value, Is.LessThan(result.Report.FirstValidHitTime.Right.Value));
            Assert.That(result.Report.FirstHitsOnSameStep, Is.True);
        }

        [Test]
        public void AKillingHit_RecordsTheDeath_AndTheDeadDummysAttackStops()
        {
            _state.Fighters[Side.Right].Hp = AlmostDeadHp;
            ScheduleHit(Side.Left, BodyPart.LeftLeg, 0.0505);
            ScheduleHit(Side.Right, BodyPart.Torso, 0.1005);

            TurnResult result = Run(StraightThrust, StraightThrust);

            Assert.That(EventsOf<FighterDiedEvent>(result).Single().Side, Is.EqualTo(Side.Right));
            Assert.That(result.Report.ResolvedState.Fighters[Side.Right].IsDead, Is.True);
            Assert.That(HpLost(result, Side.Left), Is.EqualTo(0f), "a dead dummy deals no damage");
        }

        [Test]
        public void AStrongHeadHit_StunsTheVictimsNextTurn()
        {
            _rules.Damage.HeadStunThreshold = 10f;
            ScheduleHit(Side.Left, BodyPart.Head, 0.0505);

            TurnResult result = Run(StraightThrust);

            Assert.That(EventsOf<StatusAppliedEvent>(result).Single().Side, Is.EqualTo(Side.Right));
            Assert.That(result.Report.ResolvedState.Fighters[Side.Right].Statuses.Single().Kind, Is.EqualTo(StatusKind.Stunned));
        }

        private Vec2 Tip(BodyPose grip) => grip.PositionUnits + Vec2.FromAngleDegrees(grip.RotationDegrees) * _rapier.LengthUnits;

        private float TipXInFrame(TurnResult result, double seconds)
        {
            SimTime time = SimTime.FromSeconds(seconds);
            TimelineFrame frame = result.Timeline.Frames.First(candidate => candidate.Time >= time);
            return Tip(frame.Pose.Left.HeldItem).X;
        }
    }
}
