using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using XRim.Core;
using XRim.Rules;
using XRim.Rules.Arena;
using XRim.Rules.Events;
using XRim.Rules.Limbs;
using XRim.Rules.Match;
using XRim.Rules.Paths;
using XRim.Rules.Planning;
using XRim.Rules.Settings;
using XRim.Simulation;
using XRim.Simulation.Drivers;
using XRim.Simulation.Execution;
using XRim.Simulation.Physics;
using XRim.Simulation.Recording;

namespace XRim.Tests.EditMode.Simulation.Execution
{
    /// <summary>
    /// Lost limbs inside the turn loop, on the fake world (GDD §11, §12; Session 11): severs break the joint by logic and drop the held
    /// item, the off hand fights at 80%, legs stop being posed, clubs are retrieved and dropped, a dead dummy goes limp and splits, an
    /// armless dummy headbutts, and the force-sever cheat uses the real path. Both right-handed dummies hold rapiers; the left pelvis
    /// is at x = 0, the right at x = 600. A thrust along y = 100 has the left tip at (400 + v·t, 100) and the right at (200 − v·t, 100).
    /// </summary>
    public sealed class DismembermentSimulationTests
    {
        private const float ShoulderHeight = 100f;
        private const float RightX = 600f;
        private const double HitSeconds = 0.0505;
        private const float Tolerance = 1e-3f;
        private static readonly WeaponPath StraightThrust =
            new WeaponPath(new[] { new Vec2(400f, ShoulderHeight), new Vec2(640f, ShoulderHeight) });

        private RulesSettings _rules;
        private SimulationSettings _simulation;
        private RulePolicies _policies;
        private FakePhysicsWorld _world;
        private SimClock _clock;
        private PoseSnapshot _pose;
        private MatchState _state;
        private WeaponStats _rapier;
        private ITurnContactHandler _contacts;

        [SetUp]
        public void SetUp()
        {
            _rules = GddStartingValues.CreateRulesSettings();
            _simulation = new SimulationSettings();
            _policies = new RulePolicies();
            _world = new FakePhysicsWorld();
            _clock = new SimClock(_simulation.StepRateHz);
            _rapier = _rules.FindWeapon(WeaponIds.Rapier);
            _contacts = null;
            _state = new MatchState(
                PerSide<FighterState>.Create(_ => new FighterState(Handedness.Right, _rules.Damage.MaxHp, WeaponIds.Rapier)),
                PerSide<ElectricWallState>.Create(_ => new ElectricWallState()));
            _pose = new PoseSnapshot();
            _pose.Left.Set(BodyPart.Torso, new BodyPose(Vec2.Zero, 0f));
            _pose.Right.Set(BodyPart.Torso, new BodyPose(new Vec2(RightX, 0f), 0f));
            _pose.Left.Set(BodyPart.Head, new BodyPose(new Vec2(0f, 120f), 0f));
            _pose.Right.Set(BodyPart.Head, new BodyPose(new Vec2(RightX, 120f), 0f));
            _pose.Left.HeldItem = new BodyPose(new Vec2(0f, ShoulderHeight), 0f);
            _pose.Right.HeldItem = new BodyPose(new Vec2(RightX, ShoulderHeight), 180f);
        }

        [TearDown]
        public void TearDown() => _world.Dispose();

        private FighterState Left => _state.Fighters[Side.Left];
        private FighterState Right => _state.Fighters[Side.Right];

        private static TurnPlan Plan(WeaponId weapon, WeaponPath path) => new TurnPlan(weapon, BodyMove.None, path ?? WeaponPath.Empty, default, true);

        private TurnResult Run(WeaponPath leftPath, WeaponPath rightPath = null) => Run(Left.CurrentWeapon, leftPath, Right.CurrentWeapon, rightPath);

        private TurnResult Run(WeaponId leftWeapon, WeaponPath leftPath, WeaponId rightWeapon, WeaponPath rightPath)
        {
            var options = new TurnSimulatorOptions { Contacts = _contacts ?? new HitContactHandler() };
            return new TurnSimulator(_world, _policies, options).Simulate(new TurnInput(new BoardSnapshot(_state, _pose),
                new PerSide<TurnPlan>(Plan(leftWeapon, leftPath), Plan(rightWeapon, rightPath)), _rules, _simulation));
        }

        private float TipX(Side side, double seconds)
        {
            float distance = (float)(_rapier.SpeedUnitsPerSecond * seconds);
            return side == Side.Left ? 400f + distance : RightX - 400f - distance;
        }

        /// <summary>The attacker's rapier touches a body part of the other dummy at a time; the engine reports it a step later.</summary>
        private void ScheduleHit(Side attacker, BodyPart part, double seconds)
        {
            float sign = attacker.FacingSign();
            var point = new Vec2(TipX(attacker, seconds) + sign * _rapier.InkThicknessUnits * 0.5f, ShoulderHeight);
            var facts = new ContactFacts(new BodyTag(attacker, BodyRole.HeldItem, BodyPart.Torso),
                new BodyTag(attacker.Opponent(), BodyRole.BodyPart, part), point, new Vec2(sign, 0f), new Vec2(-_rapier.SpeedUnitsPerSecond * sign, 0f));
            _world.ScheduleContacts(_clock.StepsFor(seconds) + 1, facts);
        }

        private static List<T> EventsOf<T>(TurnResult result) where T : MatchEvent => result.Timeline.Events.OfType<T>().ToList();

        /// <summary>A severed leg lying on the floor of the frozen board, along +X from its hip end.</summary>
        private void LimbOnTheFloor(Side owner, BodyPart part, Vec2 pivot)
        {
            _state.Fighters[owner].MarkSevered(part);
            _pose.SeveredLimbs.Add(new SeveredLimbPose(owner, part, new BodyPose(pivot, 90f)));
        }

        // --- Severing, dropping and the off hand (§11, §12) --------------------------------------

        [Test]
        public void ASeveringHit_BreaksTheJoint_DropsTheWeapon_AndItsAttackLandsNothing()
        {
            Right.SetLimbDamage(BodyPart.RightArm, _rules.Damage.ArmDurability - 1f);
            ScheduleHit(Side.Left, BodyPart.RightArm, HitSeconds);
            ScheduleHit(Side.Right, BodyPart.Torso, HitSeconds + 0.05);

            TurnResult result = Run(StraightThrust, StraightThrust);

            Assert.That(_world.BrokenJoints, Is.EqualTo(new[] { (Side.Right, BodyPart.RightArm) }), "§18: broken by logic");
            Assert.That(_world.Drops, Is.EqualTo(new[] { Side.Right }), "§12: the weapon drops");
            LimbSeveredEvent severed = EventsOf<LimbSeveredEvent>(result).Single();
            Assert.That((severed.Side, severed.Part), Is.EqualTo((Side.Right, BodyPart.RightArm)));
            Assert.That(EventsOf<WeaponDroppedEvent>(result).Single().Weapon, Is.EqualTo(WeaponIds.Rapier));
            Assert.That(EventsOf<HitLandedEvent>(result).Where(hit => hit.Hit.Attacker == Side.Right), Is.Empty, "a dropped weapon lands nothing");
            Assert.That(result.FinalBoard.State.Fighters[Side.Right].IsSevered(BodyPart.RightArm), Is.True);
            Assert.That(result.FinalBoard.Pose.SeveredLimbs.Single().Limb, Is.EqualTo(new SeveredLimbRef(Side.Right, BodyPart.RightArm)),
                "the limb lies on the floor of the next board");
            Assert.That(result.FinalBoard.Pose.Right.HasHeldItem, Is.False);
            Assert.That(result.FinalBoard.Pose.Right.HasDroppedItem, Is.True, "the dropped weapon lies where it fell");
            Assert.That(result.FinalBoard.Pose.Right.IsSevered(BodyPart.RightArm), Is.True, "views draw it on the floor from here");
            Assert.That(result.Timeline.Frames[0].Pose.Right.IsSevered(BodyPart.RightArm), Is.False, "on the dummy before the cut");
            Assert.That(result.FinalBoard.Pose.Left.SeveredParts, Is.EqualTo(0));
        }

        [Test]
        public void TheOffHand_HitsAtEightyPercent_AndIsMarkedSo()
        {
            Left.MarkSevered(BodyPart.RightArm);
            ScheduleHit(Side.Left, BodyPart.Torso, HitSeconds);

            HitLandedEvent hit = EventsOf<HitLandedEvent>(Run(StraightThrust)).Single();

            Assert.That(hit.Hit.IsOffHand, Is.True);
            Assert.That(hit.Damage.Damage, Is.EqualTo(_rapier.BaseDamage * _rules.Damage.OffHandDamageMultiplier).Within(Tolerance), "§12: 80%");
        }

        [Test]
        public void TheDominantHand_IsNotTheOffHand()
        {
            Left.MarkSevered(BodyPart.LeftArm);
            ScheduleHit(Side.Left, BodyPart.Torso, HitSeconds);

            Assert.That(EventsOf<HitLandedEvent>(Run(StraightThrust)).Single().Hit.IsOffHand, Is.False);
        }

        [Test]
        public void ALegSeveredMidTurn_IsNoLongerPosed()
        {
            Right.SetLimbDamage(BodyPart.LeftLeg, _rules.Damage.LegDurability - 1f);
            ScheduleHit(Side.Left, BodyPart.LeftLeg, HitSeconds);

            Run(StraightThrust);

            Assert.That(_world.BrokenJoints, Is.EqualTo(new[] { (Side.Right, BodyPart.LeftLeg) }));
            int lastLost = _world.LimbTargetSteps.Where(t => t.Side == Side.Right && t.Limb == BodyPart.LeftLeg).Max(t => t.Step);
            int lastKept = _world.LimbTargetSteps.Where(t => t.Side == Side.Right && t.Limb == BodyPart.RightLeg).Max(t => t.Step);
            Assert.That(lastLost, Is.LessThan(_clock.StepsFor(HitSeconds) + 4), "posed only until the sever");
            Assert.That(lastKept, Is.GreaterThan(lastLost + 10), "the other leg is still posed");
        }

        [Test]
        public void ADummyWithoutLegs_SitsOnItsPelvis()
        {
            Right.MarkSevered(BodyPart.LeftLeg);
            Right.MarkSevered(BodyPart.RightLeg);

            Run(StraightThrust);

            Assert.That(_world.RootTargets.Where(t => t.Side == Side.Right).Select(t => t.Pose.PositionUnits.Y), Is.All.EqualTo(LegGeometry.FloorYUnits));
            Assert.That(_world.LimbTargets.Where(t => t.Side == Side.Right), Is.Empty, "no leg left to pose");
        }

        // --- The club (§12) ----------------------------------------------------------------------

        [Test]
        public void PickingTheClub_RetrievesTheLimbFromTheFloor()
        {
            LimbOnTheFloor(Side.Right, BodyPart.LeftLeg, new Vec2(300f, 20f));
            Left.CurrentWeapon = WeaponIds.SeveredLimb;

            TurnResult result = Run(StraightThrust);

            Assert.That(_world.LoadedPose.SeveredLimbs, Is.Empty, "automatic on selection (§12 default)");
            Assert.That(_world.LoadedPose.Left.HasHeldItem, Is.True);
            Assert.That(result.FinalBoard.State.Fighters[Side.Left].HeldLimb, Is.EqualTo(new SeveredLimbRef(Side.Right, BodyPart.LeftLeg)),
                "G1: the opponent's leg");
            Assert.That(result.FinalBoard.Pose.SeveredLimbs, Is.Empty);
            Assert.That(_pose.SeveredLimbs, Has.Count.EqualTo(1), "the frozen board is never changed");
        }

        [Test]
        public void TheNearestLimb_IsRetrieved()
        {
            LimbOnTheFloor(Side.Right, BodyPart.LeftLeg, new Vec2(900f, 20f));
            LimbOnTheFloor(Side.Right, BodyPart.RightLeg, new Vec2(200f, 20f));
            Left.CurrentWeapon = WeaponIds.SeveredLimb;

            TurnResult result = Run(StraightThrust);

            Assert.That(result.FinalBoard.State.Fighters[Side.Left].HeldLimb, Is.EqualTo(new SeveredLimbRef(Side.Right, BodyPart.RightLeg)));
            Assert.That(_world.LoadedPose.SeveredLimbs.Single().Limb, Is.EqualTo(new SeveredLimbRef(Side.Right, BodyPart.LeftLeg)));
        }

        [Test]
        public void BothWantingTheLastLimb_TheNearerDummyGetsIt_AndTheOtherHoldsNothing()
        {
            LimbOnTheFloor(Side.Left, BodyPart.LeftLeg, new Vec2(450f, 20f));
            Left.CurrentWeapon = WeaponIds.SeveredLimb;
            Right.CurrentWeapon = WeaponIds.SeveredLimb;
            ScheduleHit(Side.Left, BodyPart.Torso, HitSeconds);

            TurnResult result = Run(StraightThrust, StraightThrust);

            Assert.That(result.FinalBoard.State.Fighters[Side.Right].HeldLimb, Is.EqualTo(new SeveredLimbRef(Side.Left, BodyPart.LeftLeg)));
            Assert.That(result.FinalBoard.State.Fighters[Side.Left].HeldLimb, Is.Null);
            Assert.That(_world.LoadedPose.Left.HasHeldItem, Is.False);
            Assert.That(EventsOf<HitLandedEvent>(result), Is.Empty, "empty-handed, it lands nothing");
        }

        [Test]
        public void WithinReachRetrieval_LeavesAFarLimbOnTheFloor()
        {
            _rules.Damage.LimbLoss.Retrieval = LimbRetrievalRule.WithinReach;
            LimbOnTheFloor(Side.Right, BodyPart.LeftLeg, new Vec2(900f, 20f));
            Left.CurrentWeapon = WeaponIds.SeveredLimb;

            TurnResult result = Run(StraightThrust);

            Assert.That(result.FinalBoard.State.Fighters[Side.Left].HeldLimb, Is.Null);
            Assert.That(_world.LoadedPose.SeveredLimbs, Has.Count.EqualTo(1));
            Assert.That(_world.LoadedPose.Left.HasHeldItem, Is.False);
        }

        [Test]
        public void SwitchingAwayFromTheClub_DropsItWhereItWasHeld()
        {
            var club = new BodyPose(new Vec2(100f, 150f), 30f);
            Right.MarkSevered(BodyPart.LeftLeg);
            Left.HeldLimb = new SeveredLimbRef(Side.Right, BodyPart.LeftLeg);
            _pose.Left.HeldItem = club;

            TurnResult result = Run(StraightThrust);

            SeveredLimbPose dropped = _world.LoadedPose.SeveredLimbs.Single();
            Assert.That(dropped.Limb, Is.EqualTo(new SeveredLimbRef(Side.Right, BodyPart.LeftLeg)));
            Assert.That(dropped.Pose.PositionUnits, Is.EqualTo(club.PositionUnits), "pivot at the grip");
            Assert.That(dropped.Pose.RotationDegrees, Is.EqualTo(club.RotationDegrees + 90f).Within(Tolerance), "lying along the club");
            Assert.That(dropped.HasLower, Is.True, "ten bodies: the shin too");
            Assert.That(_world.LoadedPose.Left.HeldItem, Is.EqualTo(club), "the rapier comes into the same grip");
            Assert.That(result.FinalBoard.State.Fighters[Side.Left].HeldLimb, Is.Null);
        }

        [Test]
        public void AClubDroppedWithItsArm_LiesOnTheFloorAfterTheTurn()
        {
            Right.MarkSevered(BodyPart.LeftLeg);
            Left.HeldLimb = new SeveredLimbRef(Side.Right, BodyPart.LeftLeg);
            Left.CurrentWeapon = WeaponIds.SeveredLimb;
            Left.SetLimbDamage(BodyPart.RightArm, _rules.Damage.ArmDurability - 1f);
            ScheduleHit(Side.Right, BodyPart.RightArm, HitSeconds);

            TurnResult result = Run(StraightThrust, StraightThrust);

            Assert.That(_world.Drops, Is.EqualTo(new[] { Side.Left }));
            Assert.That(result.FinalBoard.State.Fighters[Side.Left].HeldLimb, Is.Null);
            Assert.That(result.FinalBoard.Pose.SeveredLimbs.Select(limb => limb.Limb), Is.EquivalentTo(new[]
            {
                new SeveredLimbRef(Side.Right, BodyPart.LeftLeg), new SeveredLimbRef(Side.Left, BodyPart.RightArm),
            }), "the club and the arm that held it");
            Assert.That(result.FinalBoard.Pose.Left.HasDroppedItem, Is.False, "the dropped club is a floor limb again");
        }

        // --- Death (§11) -------------------------------------------------------------------------

        [Test]
        public void ADeadDummy_DropsItsWeapon_AndItsTorsoSplits()
        {
            Right.Hp = 5f;
            ScheduleHit(Side.Left, BodyPart.Torso, HitSeconds);

            TurnResult result = Run(StraightThrust, StraightThrust);

            Assert.That(EventsOf<FighterDiedEvent>(result).Single().Side, Is.EqualTo(Side.Right));
            Assert.That(_world.Drops, Is.EqualTo(new[] { Side.Right }), "S2: it lets go");
            Assert.That(_world.Splits, Is.EqualTo(new[] { Side.Right }), "§11: the torso splits in two");
            Assert.That(result.FinalBoard.Pose.Right.IsTorsoSplit, Is.True);
            int leftRoots = _world.RootTargets.Count(t => t.Side == Side.Left);
            Assert.That(_world.RootTargets.Count(t => t.Side == Side.Right), Is.LessThan(leftRoots), "limp: nothing holds it up after it died");
        }

        [Test]
        public void ASplitTorso_IsWholeAgain_ForALivingDummy()
        {
            _pose.Left.IsTorsoSplit = true;

            Run(StraightThrust);

            Assert.That(_world.LoadedPose.Left.IsTorsoSplit, Is.False, "S5: revived for sudden death");
        }

        [Test]
        public void ADroppedWeapon_IsNotRebuilt_AndTheOtherHandTakesItAtTheGuard()
        {
            Left.MarkSevered(BodyPart.RightArm);
            _pose.Left.HasHeldItem = false;
            _pose.Left.HasDroppedItem = true;

            Run(StraightThrust);

            FighterPose loaded = _world.LoadedPose.Left;
            BodyPose guard = GuardStance.HeldItemPose(_rapier, TurnStartRoot.Of(_pose.Left), Side.Left, _simulation.Ragdoll, _rules.Paths,
                new AimFromShoulderModel(), new ShieldFaceAimModel());
            Assert.That(loaded.HasDroppedItem, Is.False);
            Assert.That(loaded.HasHeldItem, Is.True, "G2: the weapon is back, in the other hand");
            Assert.That(loaded.HeldItem, Is.EqualTo(guard));
        }

        [Test]
        public void ADummyHoldingNothing_ThatDroppedNothing_StillHoldsNothing()
        {
            _pose.Right.HasHeldItem = false;
            ScheduleHit(Side.Right, BodyPart.Torso, HitSeconds);

            TurnResult result = Run(StraightThrust, StraightThrust);

            Assert.That(_world.LoadedPose.Right.HasHeldItem, Is.False, "only a drop (G2) or a club picked up puts an item in hand");
            Assert.That(EventsOf<HitLandedEvent>(result), Is.Empty);
        }

        [Test]
        public void AClubPickedUpWithNothingInHand_RestsAtTheGuard()
        {
            LimbOnTheFloor(Side.Right, BodyPart.LeftLeg, new Vec2(300f, 20f));
            Left.CurrentWeapon = WeaponIds.SeveredLimb;
            _pose.Left.HasHeldItem = false;

            Run(StraightThrust);

            BodyPose guard = GuardStance.HeldItemPose(_rules.FindWeapon(WeaponIds.SeveredLimb), TurnStartRoot.Of(_pose.Left), Side.Left,
                _simulation.Ragdoll, _rules.Paths, new AimFromShoulderModel(), new ShieldFaceAimModel());
            Assert.That(_world.LoadedPose.Left.HasHeldItem, Is.True);
            Assert.That(_world.LoadedPose.Left.HeldItem, Is.EqualTo(guard));
        }

        // --- Without arms (§12) ------------------------------------------------------------------

        [Test]
        public void AnArmlessDummy_HeadbuttsAlongItsPath()
        {
            Left.MarkSevered(BodyPart.LeftArm);
            Left.MarkSevered(BodyPart.RightArm);
            Left.CurrentWeapon = WeaponIds.BodyBlow;
            WeaponStats bodyBlow = _rules.FindWeapon(WeaponIds.BodyBlow);
            float radius = bodyBlow.InkThicknessUnits * 0.5f;
            float headCentreY = 120f + radius;
            var path = new WeaponPath(new[] { new Vec2(0f, headCentreY), new Vec2(150f, headCentreY) });
            const double seconds = 0.2;
            float centreX = (float)(bodyBlow.SpeedUnitsPerSecond * seconds);
            _world.ScheduleContacts(_clock.StepsFor(seconds) + 1, new ContactFacts(new BodyTag(Side.Left, BodyRole.BodyPart, BodyPart.Head),
                new BodyTag(Side.Right, BodyRole.BodyPart, BodyPart.Torso), new Vec2(centreX + radius, headCentreY), Vec2.UnitX,
                new Vec2(-bodyBlow.SpeedUnitsPerSecond, 0f)));

            TurnResult result = Run(path);

            Assert.That(_world.StrikingParts, Is.EqualTo(new[] { (Side.Left, BodyPart.Head) }), "§12 default: the path steers the head");
            Assert.That(_world.LoadedPose.Left.HeldItem, Is.EqualTo(HeldItemShape.HeadAsItem(_pose.Left.Get(BodyPart.Head))));
            HitLandedEvent hit = EventsOf<HitLandedEvent>(result).Single();
            Assert.That(hit.Hit.Weapon, Is.EqualTo(WeaponIds.BodyBlow));
            Assert.That(hit.Hit.Part, Is.EqualTo(BodyPart.Torso));
            Assert.That(hit.Hit.IsOffHand, Is.False, "no hand at all");
            Assert.That(hit.Damage.Damage, Is.EqualTo(bodyBlow.BaseDamage).Within(Tolerance));
            Assert.That(hit.Time.Seconds, Is.EqualTo(seconds).Within(2e-3), "timed from the head's motion");
        }

        [Test]
        public void AHeadbuttAimedLow_LeansTheBodyIn()
        {
            Left.MarkSevered(BodyPart.LeftArm);
            Left.MarkSevered(BodyPart.RightArm);
            Left.CurrentWeapon = WeaponIds.BodyBlow;
            float headCentreY = 120f + _rules.FindWeapon(WeaponIds.BodyBlow).InkThicknessUnits * 0.5f;
            var path = new WeaponPath(new[] { new Vec2(0f, headCentreY), new Vec2(150f, 90f) });

            Run(path);

            BodyPose last = _world.RootTargets.Last(target => target.Side == Side.Left).Pose;
            BodyPose lastRight = _world.RootTargets.Last(target => target.Side == Side.Right).Pose;
            Assert.That(last.RotationDegrees, Is.LessThan(-45f), "leaning toward the opponent turns the left dummy clockwise");
            Assert.That(last.PositionUnits.X, Is.GreaterThan(0f), "and steps it forward a little");
            Assert.That(lastRight.RotationDegrees, Is.EqualTo(0f).Within(Tolerance), "the other dummy stands upright");
        }

        // --- The force-sever cheat ---------------------------------------------------------------

        [Test]
        public void TheForceSeverCheat_SeversAtTheStartOfTheTurn_ThroughTheRealPath()
        {
            var cheat = new ScriptedSeverContactHandler(new HitContactHandler());
            cheat.QueueSever(Side.Right, BodyPart.RightArm);
            _contacts = cheat;
            ScheduleHit(Side.Right, BodyPart.Torso, HitSeconds);

            TurnResult result = Run(StraightThrust, StraightThrust);

            Assert.That(_world.BrokenJoints, Is.EqualTo(new[] { (Side.Right, BodyPart.RightArm) }));
            Assert.That(_world.Drops, Is.EqualTo(new[] { Side.Right }));
            Assert.That(EventsOf<LimbSeveredEvent>(result).Single().Time, Is.EqualTo(SimTime.Zero));
            Assert.That(EventsOf<WeaponDroppedEvent>(result), Has.Count.EqualTo(1));
            Assert.That(EventsOf<HitLandedEvent>(result), Is.Empty, "the dropped weapon lands nothing");
            Assert.That(result.FinalBoard.State.Fighters[Side.Right].GetLimbDamage(BodyPart.RightArm), Is.EqualTo(_rules.Damage.ArmDurability));
            Assert.That(cheat.Queued, Is.Empty, "used once");
        }
    }
}
