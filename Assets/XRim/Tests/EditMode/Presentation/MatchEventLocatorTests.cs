using NUnit.Framework;
using XRim.Core;
using XRim.Presentation.Playback;
using XRim.Rules;
using XRim.Rules.Combat;
using XRim.Rules.Damage;
using XRim.Rules.Events;
using XRim.Rules.Match;
using XRim.Rules.Settings;
using XRim.Rules.Status;
using XRim.Simulation.Execution;
using XRim.Simulation.Physics;
using XRim.Simulation.Recording;

namespace XRim.Tests.EditMode.Presentation
{
    /// <summary>Where a played event happened, for the effects that point at it (Session 13): never by changing the turn.</summary>
    public sealed class MatchEventLocatorTests
    {
        private static readonly SimTime Early = SimTime.FromSeconds(0.1);
        private static readonly SimTime Late = SimTime.FromSeconds(0.3);

        [Test]
        public void AClashOrABlock_IsWhereTheItemsMet()
        {
            var clash = new WeaponClashEvent(Late, new ClashResult(ClashKind.BothRebound, null, 70f, 5f, 5f), new Vec2(310f, 140f));

            Assert.That(MatchEventLocator.TryLocate(Turn(clash), clash, out Vec2 point), Is.True);
            Assert.That(point, Is.EqualTo(new Vec2(310f, 140f)));
        }

        [Test]
        public void AHit_IsWhereTheBladeTouchedThePart_FromTheTurnsRawContacts()
        {
            HitLandedEvent hit = Hit(BodyPart.Head);
            var contact = new ContactEvent(new TurnContact(new ContactFacts(new BodyTag(Side.Left, BodyRole.HeldItem, BodyPart.Torso),
                new BodyTag(Side.Right, BodyRole.BodyPart, BodyPart.Head), new Vec2(512f, 260f), Vec2.UnitX, Vec2.Zero), 72, Late, Side.Left, 300f, 80f));

            Assert.That(MatchEventLocator.TryLocate(Turn(contact, hit), hit, out Vec2 point), Is.True);
            Assert.That(point, Is.EqualTo(new Vec2(512f, 260f)));
        }

        [Test]
        public void AHitWithoutAContact_IsAtTheStruckPart()
        {
            HitLandedEvent hit = Hit(BodyPart.Torso);

            Assert.That(MatchEventLocator.TryLocate(Turn(hit), hit, out Vec2 point), Is.True);
            Assert.That(point, Is.EqualTo(new Vec2(600f, 60f)), "the torso's middle: halfway from the pelvis to the neck");
        }

        [Test]
        public void ASever_IsAtTheLimbsJoint_AsThePoseShowsItThen()
        {
            var sever = new LimbSeveredEvent(Late, Side.Right, BodyPart.LeftArm);

            Assert.That(MatchEventLocator.TryLocate(Turn(sever), sever, out Vec2 point), Is.True);
            Assert.That(point, Is.EqualTo(new Vec2(605f, 100f)), "the shoulder in the late frame");
        }

        [Test]
        public void ACounterHit_IsWhereItsHitLanded()
        {
            HitLandedEvent hit = Hit(BodyPart.Torso);
            var interrupted = new AttackInterruptedEvent(Late, Side.Right);

            Assert.That(MatchEventLocator.InterruptingHit(Turn(hit, interrupted), interrupted), Is.SameAs(hit));
            Assert.That(MatchEventLocator.TryLocate(Turn(hit, interrupted), interrupted, out Vec2 point), Is.True);
            Assert.That(point, Is.EqualTo(new Vec2(600f, 60f)));
        }

        [Test]
        public void TheShownPose_IsTheLastFrameAtOrBeforeTheTime()
        {
            TurnResult turn = Turn();

            Assert.That(MatchEventLocator.PoseAt(turn.Timeline, SimTime.FromSeconds(0.05)), Is.SameAs(turn.Timeline.Frames[0].Pose));
            Assert.That(MatchEventLocator.PoseAt(turn.Timeline, SimTime.FromSeconds(0.2)), Is.SameAs(turn.Timeline.Frames[1].Pose));
            Assert.That(MatchEventLocator.PoseAt(turn.Timeline, SimTime.FromSeconds(2.0)), Is.SameAs(turn.Timeline.Frames[2].Pose));
        }

        [Test]
        public void AnEventWithNoPlace_IsNotLocated()
        {
            var status = new StatusAppliedEvent(Late, Side.Right, StatusKind.Stunned);

            Assert.That(MatchEventLocator.TryLocate(Turn(status), status, out _), Is.False);
        }

        private static HitLandedEvent Hit(BodyPart part) =>
            new HitLandedEvent(Late, new HitFacts(Side.Left, part, WeaponIds.Rapier, Late, false), new DamageResult(8f, 8f, 0f, false, false));

        /// <summary>Three frames (0, 0.1 s, 0.3 s); the right dummy's pelvis at (600, 0), neck at (600, 120), left shoulder moving.</summary>
        private static TurnResult Turn(params MatchEvent[] events)
        {
            RulesSettings settings = GddStartingValues.CreateRulesSettings();
            BoardSnapshot board = TestData.CreateInitialBoard(settings);
            var recorder = new TimelineRecorder();
            recorder.RecordFrame(0, SimTime.Zero, Pose(595f));
            recorder.RecordFrame(24, Early, Pose(600f));
            recorder.RecordFrame(72, Late, Pose(605f));
            foreach (MatchEvent matchEvent in events)
            {
                recorder.RecordEvent(matchEvent);
            }

            return new TurnResult(0, recorder.Build(), board, new ExecutionReport(board.State, new PerSide<SimTime?>(null, null), false));
        }

        private static PoseSnapshot Pose(float leftShoulderX)
        {
            var pose = new PoseSnapshot();
            pose.Right.Set(BodyPart.Torso, new BodyPose(new Vec2(600f, 0f), 0f));
            pose.Right.Set(BodyPart.Head, new BodyPose(new Vec2(600f, 120f), 0f));
            pose.Right.Set(BodyPart.LeftArm, new BodyPose(new Vec2(leftShoulderX, 100f), 0f));
            return pose;
        }
    }
}
