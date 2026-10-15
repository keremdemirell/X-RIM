using NUnit.Framework;
using XRim.Core;
using XRim.Presentation.Moments;
using XRim.Rules;
using XRim.Rules.Combat;
using XRim.Rules.Damage;
using XRim.Rules.Events;
using XRim.Rules.Match;
using XRim.Rules.Settings;
using XRim.Simulation.Physics;
using XRim.Simulation.Recording;

namespace XRim.Tests.EditMode.Presentation
{
    /// <summary>
    /// Which turns are legendary (Session 13; GDD pillar 1, §17 local instant replay): the §1 reference moment (duck under a mace swing and
    /// thrust the rapier into the pelvis in the same turn), a counter-hit, a crush, a same-instant trade, a sever, a KO (always: the
    /// match-winning blow), a double KO and a sudden-death finish; the most important wins; ordinary turns are not.
    /// </summary>
    public sealed class MomentDetectorTests
    {
        private static readonly SimTime Early = SimTime.FromSeconds(0.25);
        private static readonly SimTime Late = SimTime.FromSeconds(0.6);

        private readonly HeuristicMomentDetector _detector = new HeuristicMomentDetector();

        // --- The §1 reference moment ---------------------------------------------------------------

        [Test]
        public void DuckingUnderAMaceSwing_AndThrustingIntoThePelvis_IsTheReferenceMoment()
        {
            TurnResult turn = Turn(
                new BodyMoveStartedEvent(SimTime.Zero, Side.Left, BodyMove.Crouch),
                new AttackStartedEvent(SimTime.Zero, Side.Left, WeaponIds.Rapier, WeaponKind.Weapon),
                new AttackStartedEvent(SimTime.Zero, Side.Right, WeaponIds.Mace, WeaponKind.Weapon),
                Hit(Side.Left, BodyPart.Torso, Early));

            Assert.That(_detector.TryDetect(turn, out LegendaryMoment moment), Is.True);
            Assert.That(moment.Kind, Is.EqualTo(MomentKind.DodgeCounter));
            Assert.That(moment.Hero, Is.EqualTo(Side.Left));
            Assert.That(moment.Time, Is.EqualTo(Early), "centred on the counter's hit");
            Assert.That(moment.FocusUnits, Is.EqualTo(new Vec2(600f, 60f)), "where it landed: the struck torso");
            Assert.That(moment.Reason, Does.Contain("ducked"));
        }

        [TestCase(BodyMove.Jump)]
        [TestCase(BodyMove.StepBack)]
        public void JumpingOrSlippingBackOutOfTheSwing_AlsoCounts(BodyMove dodge)
        {
            TurnResult turn = Turn(
                new BodyMoveStartedEvent(SimTime.Zero, Side.Right, dodge),
                new AttackStartedEvent(SimTime.Zero, Side.Left, WeaponIds.Mace, WeaponKind.Weapon),
                Hit(Side.Right, BodyPart.Head, Early));

            Assert.That(_detector.TryDetect(turn, out LegendaryMoment moment), Is.True);
            Assert.That(moment.Kind, Is.EqualTo(MomentKind.DodgeCounter));
            Assert.That(moment.Hero, Is.EqualTo(Side.Right));
        }

        [Test]
        public void ItIsNoDodge_WhenTheSwingAlsoLanded_OrWasOnlyAShield_OrNobodyMoved()
        {
            TurnResult bothHit = Turn(
                new BodyMoveStartedEvent(SimTime.Zero, Side.Left, BodyMove.Crouch),
                new AttackStartedEvent(SimTime.Zero, Side.Right, WeaponIds.Mace, WeaponKind.Weapon),
                Hit(Side.Left, BodyPart.Torso, Early), Hit(Side.Right, BodyPart.Head, Late));
            TurnResult shield = Turn(
                new BodyMoveStartedEvent(SimTime.Zero, Side.Left, BodyMove.Crouch),
                new AttackStartedEvent(SimTime.Zero, Side.Right, WeaponIds.Shield, WeaponKind.Shield),
                Hit(Side.Left, BodyPart.Torso, Early));
            TurnResult standing = Turn(
                new AttackStartedEvent(SimTime.Zero, Side.Right, WeaponIds.Mace, WeaponKind.Weapon),
                Hit(Side.Left, BodyPart.Torso, Early));
            TurnResult lunge = Turn(
                new BodyMoveStartedEvent(SimTime.Zero, Side.Left, BodyMove.Lunge),
                new AttackStartedEvent(SimTime.Zero, Side.Right, WeaponIds.Mace, WeaponKind.Weapon),
                Hit(Side.Left, BodyPart.Torso, Early));

            Assert.That(_detector.TryDetect(bothHit, out _), Is.False);
            Assert.That(_detector.TryDetect(shield, out _), Is.False);
            Assert.That(_detector.TryDetect(standing, out _), Is.False);
            Assert.That(_detector.TryDetect(lunge, out _), Is.False, "a lunge is no dodge");
        }

        // --- The other kinds -----------------------------------------------------------------------

        [Test]
        public void AHitThatCancelsTheOthersAttack_IsACounterHit_UnlessTheOtherLandedToo()
        {
            TurnResult counter = Turn(Hit(Side.Left, BodyPart.RightArm, Early), new AttackInterruptedEvent(Early, Side.Right));
            Assert.That(_detector.TryDetect(counter, out LegendaryMoment moment), Is.True);
            Assert.That(moment.Kind, Is.EqualTo(MomentKind.CounterHit));
            Assert.That(moment.Hero, Is.EqualTo(Side.Left));

            TurnResult exchange = Turn(Hit(Side.Right, BodyPart.Torso, SimTime.FromSeconds(0.1)), Hit(Side.Left, BodyPart.RightArm, Early),
                new AttackInterruptedEvent(Early, Side.Right));
            Assert.That(_detector.TryDetect(exchange, out _), Is.False);
        }

        [Test]
        public void HitsAtExactlyTheSameInstant_AreATrade()
        {
            TurnResult turn = Turn(Hit(Side.Left, BodyPart.Torso, Early), Hit(Side.Right, BodyPart.Torso, Early));

            Assert.That(_detector.TryDetect(turn, out LegendaryMoment moment), Is.True);
            Assert.That(moment.Kind, Is.EqualTo(MomentKind.Trade));
            Assert.That(moment.Hero, Is.Null, "nobody alone");
        }

        [Test]
        public void ACrushThrough_IsACrush_ByItsWinner()
        {
            TurnResult turn = Turn(new WeaponClashEvent(Early, new ClashResult(ClashKind.CrushThrough, Side.Right, 70f, 4f, 12f), new Vec2(310f, 140f)));

            Assert.That(_detector.TryDetect(turn, out LegendaryMoment moment), Is.True);
            Assert.That(moment.Kind, Is.EqualTo(MomentKind.Crush));
            Assert.That(moment.Hero, Is.EqualTo(Side.Right));
            Assert.That(moment.FocusUnits, Is.EqualTo(new Vec2(310f, 140f)));
        }

        [Test]
        public void ASever_IsLegendary_ForTheOneWhoCut()
        {
            TurnResult turn = Turn(new LimbSeveredEvent(Early, Side.Right, BodyPart.LeftArm));

            Assert.That(_detector.TryDetect(turn, out LegendaryMoment moment), Is.True);
            Assert.That(moment.Kind, Is.EqualTo(MomentKind.Sever));
            Assert.That(moment.Hero, Is.EqualTo(Side.Left));
        }

        [Test]
        public void EveryKO_IsReplayed_TheMatchWinningBlow()
        {
            TurnResult turn = Turn(Hit(Side.Left, BodyPart.Head, Late), new FighterDiedEvent(Late, Side.Right));

            Assert.That(_detector.TryDetect(turn, out LegendaryMoment moment), Is.True);
            Assert.That(moment.Kind, Is.EqualTo(MomentKind.KnockOut));
            Assert.That(moment.Hero, Is.EqualTo(Side.Left));
            Assert.That(moment.Time, Is.EqualTo(Late));
        }

        [Test]
        public void BothDying_IsADoubleKO()
        {
            TurnResult turn = Turn(new FighterDiedEvent(Early, Side.Right), new FighterDiedEvent(Late, Side.Left));

            Assert.That(_detector.TryDetect(turn, out LegendaryMoment moment), Is.True);
            Assert.That(moment.Kind, Is.EqualTo(MomentKind.DoubleKnockOut));
            Assert.That(moment.Hero, Is.Null);
        }

        [Test]
        public void ASuddenDeathTurn_IsDecidedByItsFirstValidHit()
        {
            TurnResult turn = Turn(true, new PerSide<SimTime?>(Late, Early), new PerSide<SimTime?>(Early, null),
                Hit(Side.Right, BodyPart.Torso, Early), new FighterDiedEvent(Early, Side.Left), Hit(Side.Left, BodyPart.Torso, Late));

            Assert.That(_detector.TryDetect(turn, out LegendaryMoment moment), Is.True);
            Assert.That(moment.Kind, Is.EqualTo(MomentKind.SuddenDeathFinish));
            Assert.That(moment.Hero, Is.EqualTo(Side.Right));
            Assert.That(moment.Time, Is.EqualTo(Early));
        }

        [Test]
        public void ASuddenDeathTie_IsNobodysAlone()
        {
            TurnResult turn = Turn(true, new PerSide<SimTime?>(Early, Early), new PerSide<SimTime?>(Early, Early),
                Hit(Side.Left, BodyPart.Torso, Early), Hit(Side.Right, BodyPart.Torso, Early));

            Assert.That(_detector.TryDetect(turn, out LegendaryMoment moment), Is.True);
            Assert.That(moment.Kind, Is.EqualTo(MomentKind.SuddenDeathFinish));
            Assert.That(moment.Hero, Is.Null);
        }

        // --- Importance and switches ---------------------------------------------------------------

        [Test]
        public void TheMostImportantMoment_Wins()
        {
            TurnResult turn = Turn(
                new BodyMoveStartedEvent(SimTime.Zero, Side.Left, BodyMove.Crouch),
                new AttackStartedEvent(SimTime.Zero, Side.Right, WeaponIds.Mace, WeaponKind.Weapon),
                new WeaponClashEvent(SimTime.FromSeconds(0.05), new ClashResult(ClashKind.CrushThrough, Side.Left, 70f, 12f, 4f), Vec2.Zero),
                Hit(Side.Left, BodyPart.LeftArm, Early), new LimbSeveredEvent(Early, Side.Right, BodyPart.LeftArm),
                Hit(Side.Left, BodyPart.Head, Late), new FighterDiedEvent(Late, Side.Right));

            Assert.That(_detector.TryDetect(turn, out LegendaryMoment moment), Is.True);
            Assert.That(moment.Kind, Is.EqualTo(MomentKind.KnockOut), "a KO before a sever, a dodge or a crush");
        }

        [Test]
        public void ASwitchedOffKind_IsPassedOver_ForTheNextOne()
        {
            var detector = new HeuristicMomentDetector(new MomentSettings { KnockOuts = false });
            TurnResult turn = Turn(new LimbSeveredEvent(Early, Side.Right, BodyPart.LeftArm), new FighterDiedEvent(Late, Side.Right));

            Assert.That(detector.TryDetect(turn, out LegendaryMoment moment), Is.True);
            Assert.That(moment.Kind, Is.EqualTo(MomentKind.Sever));

            var nothingOn = new HeuristicMomentDetector(new MomentSettings
            {
                KnockOuts = false, Severs = false, SuddenDeathFinishes = false, DodgeCounters = false, CounterHits = false, Trades = false, Crushes = false,
            });
            Assert.That(nothingOn.TryDetect(turn, out _), Is.False);
        }

        [Test]
        public void AnOrdinaryTurn_IsNotLegendary()
        {
            TurnResult turn = Turn(
                new AttackStartedEvent(SimTime.Zero, Side.Left, WeaponIds.Rapier, WeaponKind.Weapon),
                new AttackStartedEvent(SimTime.Zero, Side.Right, WeaponIds.Mace, WeaponKind.Weapon),
                Hit(Side.Left, BodyPart.LeftLeg, Early),
                new WeaponClashEvent(Late, new ClashResult(ClashKind.BothRebound, null, 70f, 5f, 5f), Vec2.Zero));

            Assert.That(_detector.TryDetect(turn, out _), Is.False);
        }

        // --- The replay's stretch ------------------------------------------------------------------

        [Test]
        public void TheReplay_ShowsALeadBeforeTheMomentAndATailAfter_WithinTheTurn()
        {
            var settings = new ReplaySettings { LeadSeconds = 0.3f, TailSeconds = 0.4f };

            settings.WindowAround(SimTime.FromSeconds(0.5), SimTime.FromSeconds(1.5), out SimTime start, out SimTime end);
            Assert.That(start.Seconds, Is.EqualTo(0.2).Within(1e-6));
            Assert.That(end.Seconds, Is.EqualTo(0.9).Within(1e-6));

            settings.WindowAround(SimTime.FromSeconds(0.1), SimTime.FromSeconds(0.3), out start, out end);
            Assert.That(start, Is.EqualTo(SimTime.Zero), "not before the turn");
            Assert.That(end.Seconds, Is.EqualTo(0.3).Within(1e-6), "not past it");
        }

        // --- A replay asked for (the debug Playback tab) -------------------------------------------

        [Test]
        public void AReplayAskedFor_ShowsTheTurnsLegendaryMoment_WhenItHasOne()
        {
            TurnResult turn = Turn(new WeaponClashEvent(Early, new ClashResult(ClashKind.CrushThrough, Side.Right, 70f, 4f, 12f), new Vec2(310f, 140f)));

            LegendaryMoment moment = RequestedMoment.Pick(turn, _detector);

            Assert.That(moment.Kind, Is.EqualTo(MomentKind.Crush));
        }

        [Test]
        public void AReplayAskedFor_OfAnOrdinaryTurn_ShowsItsHardestHit()
        {
            var light = new HitLandedEvent(Early, new HitFacts(Side.Left, BodyPart.LeftLeg, WeaponIds.Rapier, Early, false),
                new DamageResult(4f, 4f, 0f, false, false));
            var hard = new HitLandedEvent(Late, new HitFacts(Side.Right, BodyPart.Torso, WeaponIds.Mace, Late, false),
                new DamageResult(12f, 12f, 0f, false, false));
            TurnResult turn = Turn(light, hard);
            Assert.That(_detector.TryDetect(turn, out _), Is.False, "nothing legendary");

            LegendaryMoment moment = RequestedMoment.Pick(turn, _detector);

            Assert.That(moment.Kind, Is.EqualTo(MomentKind.Requested));
            Assert.That(moment.Time, Is.EqualTo(Late));
            Assert.That(moment.Hero, Is.EqualTo(Side.Right));
        }

        [Test]
        public void AReplayAskedFor_OfATurnWithoutHits_ShowsItsMiddle_BetweenTheDummies()
        {
            LegendaryMoment moment = RequestedMoment.Pick(Turn(), _detector);

            Assert.That(moment.Kind, Is.EqualTo(MomentKind.Requested));
            Assert.That(moment.Time.Seconds, Is.EqualTo(0.5).Within(1e-6));
            Assert.That(moment.Hero, Is.Null);
            Assert.That(moment.FocusUnits, Is.EqualTo(new Vec2(300f, 60f)), "between the two torsos' middles");
        }

        [Test]
        public void AReplayAskedFor_IsNeverSwitchedOff()
        {
            var nothingOn = new MomentSettings
            {
                KnockOuts = false, Severs = false, SuddenDeathFinishes = false, DodgeCounters = false, CounterHits = false, Trades = false, Crushes = false,
            };

            Assert.That(nothingOn.Allows(MomentKind.Requested), Is.True);
        }

        private static HitLandedEvent Hit(Side attacker, BodyPart part, SimTime time) =>
            new HitLandedEvent(time, new HitFacts(attacker, part, WeaponIds.Rapier, time, false), new DamageResult(10f, 10f, 0f, false, false));

        private static TurnResult Turn(params MatchEvent[] events) => Turn(false, new PerSide<SimTime?>(null, null), new PerSide<SimTime?>(null, null), events);

        /// <summary>A turn with these events; the left dummy's pelvis at 0, the right's at 600 (neck 120 above).</summary>
        private static TurnResult Turn(bool suddenDeath, PerSide<SimTime?> firstHits, PerSide<SimTime?> deaths, params MatchEvent[] events)
        {
            RulesSettings settings = GddStartingValues.CreateRulesSettings();
            BoardSnapshot board = TestData.CreateInitialBoard(settings);
            board.State.IsSuddenDeath = suddenDeath;
            var pose = new PoseSnapshot();
            pose.Left.Set(BodyPart.Torso, new BodyPose(Vec2.Zero, 0f));
            pose.Left.Set(BodyPart.Head, new BodyPose(new Vec2(0f, 120f), 0f));
            pose.Right.Set(BodyPart.Torso, new BodyPose(new Vec2(600f, 0f), 0f));
            pose.Right.Set(BodyPart.Head, new BodyPose(new Vec2(600f, 120f), 0f));
            var recorder = new TimelineRecorder();
            recorder.RecordFrame(0, SimTime.Zero, pose);
            recorder.RecordFrame(1, SimTime.FromSeconds(1.0), pose);
            foreach (MatchEvent matchEvent in events)
            {
                recorder.RecordEvent(matchEvent);
            }

            return new TurnResult(0, recorder.Build(), board, new ExecutionReport(board.State, firstHits, false, deaths));
        }
    }
}
