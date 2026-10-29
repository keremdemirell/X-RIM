using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using XRim.Core;
using XRim.Networking.Protocol;
using XRim.Rules;
using XRim.Rules.Paths;
using XRim.Rules.Planning;
using XRim.Rules.Settings;
using XRim.Simulation.Recording;
using XRim.Tests.EditMode.Simulation.Scenarios;

namespace XRim.Tests.EditMode.Networking.Server
{
    /// <summary>
    /// The server checks every input on its own clock (GDD §18): a stroke past the ink budget never executes past it, a weapon switch that
    /// reaches it inside the lock-out is refused, Ready after the deadline is refused, and a client can never send a result (a forged one is
    /// refused and changes nothing). Messages it cannot trust end the connection with the reason.
    /// </summary>
    public sealed class ServerValidationTests
    {
        private const double LockoutMarginSeconds = 0.5;

        [Test]
        public void AStrokePastTheInkBudget_NeverExecutesPastIt()
        {
            var harness = new ServerHarness();
            (TestPlayer left, TestPlayer right) = harness.StartMatch();
            WeaponStats rapier = harness.Rules.FindWeapon(WeaponIds.Rapier);
            WeaponPath loops = Loops(rapier.InkLengthUnits * 5f);

            int sequence = left.Command(new SetPathCommand(loops));
            harness.Tick();
            left.Read();
            harness.PlayTurn(left, right);

            Assert.That(left.All<CommandAnswerMessage>().Single(a => a.Sequence == sequence).Accepted, Is.True,
                "an over-long stroke is cut where the ink runs out, as on the phone, never refused");
            TurnPlan executed = harness.Simulator.Inputs[0].Plans.Left;
            float cost = new RulePolicies().InkCost.Measure(executed.Path, rapier).CostUnits;
            Assert.That(loops.LengthUnits, Is.GreaterThan(rapier.InkLengthUnits * 4f), "the stroke drawn was far over budget");
            Assert.That(executed.Path.IsEmpty, Is.False);
            Assert.That(cost, Is.LessThanOrEqualTo(rapier.InkLengthUnits + 0.01f), "what executes costs no more than the weapon's ink");
        }

        [Test]
        public void NoClientMessage_CanCarryABuiltPath_AWholePlan_OrAResult()
        {
            var forbidden = new[] { typeof(TurnPlan), typeof(BuiltPath), typeof(TurnResult), typeof(TurnTimeline), typeof(BoardSnapshot) };
            IEnumerable<Type> clientMessages = typeof(NetMessage).Assembly.GetTypes()
                .Where(type => typeof(NetMessage).IsAssignableFrom(type) && !type.IsAbstract)
                .Where(type => MessageKinds.IsSentByClient(KindOf(type)));
            IEnumerable<Type> carried = clientMessages.Concat(ProtocolCodec.SupportedCommandTypes)
                .SelectMany(type => type.GetProperties(BindingFlags.Instance | BindingFlags.Public)).Select(property => property.PropertyType);

            Assert.That(carried.Intersect(forbidden), Is.Empty);
        }

        [Test]
        public void AWeaponSwitchReachingTheServerInsideTheLockout_IsRefused_AndThePlayerGetsTheirPlanBack()
        {
            var harness = new ServerHarness();
            (TestPlayer left, TestPlayer right) = harness.StartMatch();
            float planning = harness.Rules.Match.PlanningDurationSeconds;
            float lockout = harness.Rules.Match.WeaponSwitchLockoutSeconds;

            harness.Advance(planning - lockout - LockoutMarginSeconds);
            int inTime = left.Command(new SelectWeaponCommand(WeaponIds.Mace));
            harness.Tick();
            harness.Advance(LockoutMarginSeconds * 2);
            int late = left.Command(new SelectWeaponCommand(WeaponIds.Shield));
            harness.Tick();
            ServerHarness.Read(left, right);

            Assert.That(left.All<CommandAnswerMessage>().Single(a => a.Sequence == inTime).Accepted, Is.True);
            Assert.That(left.All<CommandAnswerMessage>().Single(a => a.Sequence == late).Rejection, Is.EqualTo(CommandRejection.WeaponSwitchLockedOut));
            Assert.That(left.Last<PlanStateMessage>().Weapon, Is.EqualTo(WeaponIds.Mace), "the phone is told the plan the server holds");
            Assert.That(right.All<PublicStateMessage>().Any(m => m.Side == Side.Left && m.State.Weapon == WeaponIds.Shield), Is.False);

            harness.Advance(planning);
            Assert.That(harness.Simulator.Inputs[0].Plans.Left.Weapon, Is.EqualTo(WeaponIds.Mace));
        }

        [Test]
        public void ReadyReachingTheServerAtTheDeadline_IsRefused()
        {
            var harness = new ServerHarness();
            (TestPlayer left, _) = harness.StartMatch();
            harness.Clock.Advance(harness.Rules.Match.PlanningDurationSeconds);

            int ready = left.Command(new SetReadyCommand(true));
            harness.Tick();
            left.Read();

            Assert.That(left.All<CommandAnswerMessage>().Single(a => a.Sequence == ready).Rejection, Is.EqualTo(CommandRejection.NotInPlanningPhase));
            Assert.That(harness.Simulator.Inputs[0].Plans.Left.PressedReady, Is.False);
        }

        [Test]
        public void ACommandForAnotherTurn_IsRefused()
        {
            var harness = new ServerHarness();
            (TestPlayer left, _) = harness.StartMatch();

            int stale = left.Command(new SetBodyMoveCommand(BodyMove.Crouch), 5);
            harness.Tick();
            left.Read();

            Assert.That(left.All<CommandAnswerMessage>().Single(a => a.Sequence == stale).Rejection, Is.EqualTo(CommandRejection.NotInPlanningPhase));
        }

        [Test]
        public void AForgedResult_IsRefused_TheSenderIsDisconnected_AndTheMatchGoesOnWithTheServersOwn()
        {
            var harness = new ServerHarness();
            (TestPlayer left, TestPlayer right) = harness.StartMatch();
            TurnResult forged = ScenarioTestData.TurnWithEveryEvent(); // a turn in which the right dummy dies

            left.Send(new TurnResolvedMessage(forged));
            harness.Tick();
            ServerHarness.Read(left, right);

            Assert.That(left.Last<GoodbyeMessage>().Reason, Is.EqualTo(GoodbyeReason.ProtocolViolation));
            Assert.That(left.IsOpen, Is.False);
            Assert.That(right.All<TurnResolvedMessage>(), Is.Empty, "nothing a client sends reaches the other player as a result");
            Assert.That(right.Last<ConnectionNoticeMessage>().Status, Is.EqualTo(ConnectionStatus.Dropped));

            right.Command(new SetReadyCommand(true));
            harness.Advance(harness.Rules.Match.PlanningDurationSeconds);
            right.Read();
            TurnResult result = right.Last<TurnResolvedMessage>().Result;
            Assert.That(harness.Simulator.Inputs.Count, Is.EqualTo(1));
            Assert.That(result.FinalBoard.State.Fighters.Right.Hp, Is.EqualTo(harness.Rules.Damage.MaxHp));
        }

        [Test]
        public void AnUnreadableMessage_IsRefused_AsAProtocolViolation()
        {
            var harness = new ServerHarness();
            (TestPlayer left, _) = harness.StartMatch();

            left.SendBytes(new byte[] { 1, 0, 3, 0, 0xFF, 0xFF, 0xFF });
            harness.Tick();
            left.Read();

            Assert.That(left.Last<GoodbyeMessage>().Reason, Is.EqualTo(GoodbyeReason.ProtocolViolation));
            Assert.That(left.IsOpen, Is.False);
        }

        [Test]
        public void AnotherProtocolVersion_IsRefused_AsAVersionMismatch()
        {
            var harness = new ServerHarness();
            TestPlayer player = harness.Connect();
            byte[] hello = ProtocolCodec.Encode(new ClientHelloMessage(Handedness.Right, new SignatureMoveId[0]));
            hello[0] = (byte)(ProtocolCodec.CurrentVersion + 1);

            player.SendBytes(hello);
            harness.Tick();
            player.Read();

            Assert.That(player.Last<GoodbyeMessage>().Reason, Is.EqualTo(GoodbyeReason.VersionMismatch));
            Assert.That(harness.Server.WaitingPlayers, Is.EqualTo(0));
        }

        [Test]
        public void AConnection_MustSayHelloFirst()
        {
            var harness = new ServerHarness();
            TestPlayer player = harness.Connect();

            player.Send(new PlaybackFinishedMessage(0));
            harness.Tick();
            player.Read();

            Assert.That(player.Last<GoodbyeMessage>().Reason, Is.EqualTo(GoodbyeReason.ProtocolViolation));
        }

        [Test]
        public void AnInvalidLoadoutPick_IsRefused()
        {
            var harness = new ServerHarness();
            TestPlayer left = harness.Join();
            harness.Join();
            left.Read();

            left.Pick(WeaponIds.Rapier, WeaponIds.Rapier);
            harness.Tick();
            left.Read();

            Assert.That(left.Last<GoodbyeMessage>().Reason, Is.EqualTo(GoodbyeReason.ProtocolViolation));
        }

        /// <summary>Circles inside the rapier's reach, drawn round and round until the stroke is this long.</summary>
        private static WeaponPath Loops(float lengthUnits)
        {
            const float radius = 100f;
            var centre = new Vec2(250f, 100f);
            var points = new List<Vec2>();
            int count = (int)Math.Ceiling(lengthUnits);
            for (int i = 0; i <= count && i < ProtocolLimits.MaxStrokePoints; i++)
            {
                double angle = i / radius;
                points.Add(centre + new Vec2((float)Math.Cos(angle) * radius, (float)Math.Sin(angle) * radius));
            }

            return new WeaponPath(points);
        }

        private static MessageKind KindOf(Type messageType) =>
            ((NetMessage)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(messageType)).Kind;
    }
}
