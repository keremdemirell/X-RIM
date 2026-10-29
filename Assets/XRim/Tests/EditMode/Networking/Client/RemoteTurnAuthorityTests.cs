using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using XRim.Core;
using XRim.Networking;
using XRim.Networking.Client;
using XRim.Networking.Protocol;
using XRim.Networking.Transport;
using XRim.Rules;
using XRim.Rules.Match;
using XRim.Rules.Planning;
using XRim.Simulation;
using XRim.Simulation.Execution;
using XRim.Tests.EditMode.Networking.Server;

namespace XRim.Tests.EditMode.Networking.Client
{
    /// <summary>
    /// The phone's side of an online match (Session 19): through <see cref="RemoteTurnAuthority"/> the client code above
    /// <see cref="ITurnAuthority"/> works unchanged. Bots play whole matches through the server and plan exactly what they plan offline,
    /// the events arrive in the offline order, the countdown runs early by the round trip and the margin, the phone sends only what its own
    /// copy of the rules accepts and is put right when the server refuses, and a dropped link rejoins by itself.
    /// </summary>
    public sealed class RemoteTurnAuthorityTests
    {
        private const int KnockOutTurn = 4;

        [Test]
        public void TwoBots_PlayAWholeMatch_ThroughTheServer()
        {
            var run = new OnlineMatchRun(KnockOutTurn);

            run.Run();

            foreach (OnlineBotClient client in new[] { run.Left, run.Right })
            {
                Assert.That(client.Outcome, Is.Not.Null, "the match ended");
                Assert.That(client.Outcome.Winner, Is.EqualTo(Side.Left));
                Assert.That(client.Outcome.Reason, Is.EqualTo(MatchEndReason.KnockOut));
                Assert.That(client.Results.Select(r => r.TurnIndex), Is.EqualTo(Enumerable.Range(0, KnockOutTurn + 1)));
                Assert.That(client.Link.EndReason, Is.EqualTo(GoodbyeReason.MatchOver));
            }

            Assert.That(run.Simulator.Inputs.Count, Is.EqualTo(KnockOutTurn + 1), "only the server simulated");
        }

        [Test]
        public void TheSameBots_PlanTheSamePlans_OnlineAndOffline()
        {
            var run = new OnlineMatchRun(KnockOutTurn);
            run.Run();
            (ScriptedTurnSimulator offline, _) = OnlineMatchRun.PlayOffline(KnockOutTurn);

            IReadOnlyList<TurnInput> online = run.Simulator.Inputs;
            Assert.That(online.Count, Is.EqualTo(offline.Inputs.Count));
            for (int turn = 0; turn < online.Count; turn++)
            {
                foreach (Side side in new[] { Side.Left, Side.Right })
                {
                    TurnPlan a = offline.Inputs[turn].Plans[side];
                    TurnPlan b = online[turn].Plans[side];
                    string where = $"turn {turn}, {side}";
                    Assert.That(b.Weapon, Is.EqualTo(a.Weapon), where);
                    Assert.That(b.BodyMove, Is.EqualTo(a.BodyMove), where);
                    Assert.That(b.Signature, Is.EqualTo(a.Signature), where);
                    Assert.That(b.PressedReady, Is.EqualTo(a.PressedReady), where);
                    Assert.That(b.Path.Points, Is.EqualTo(a.Path.Points), where + ": the server built the same path from the raw stroke");
                }
            }
        }

        [Test]
        public void TheEvents_ArriveInTheOfflineOrder()
        {
            var run = new OnlineMatchRun(KnockOutTurn);
            run.Run();
            (_, List<string> offline) = OnlineMatchRun.PlayOffline(KnockOutTurn);

            Assert.That(run.Left.Events, Is.EqualTo(offline));
            Assert.That(run.Right.Events, Is.EqualTo(offline));
        }

        [Test]
        public void TheCountdown_RunsEarly_ByTheRoundTripAndTheMargin()
        {
            var harness = new ServerHarness();
            var clock = new ManualClock(100.0);
            var join = new OnlineMatchJoin(new ServerLink(() => harness.Loopback.Connect(), clock, harness.Network), Hello());
            harness.Tick();
            clock.Advance(0.2); // the answer to the first ping takes 0.2 s to come back
            join.Tick();
            RemoteTurnAuthority authority = StartAgainst(harness, join, clock, out _);
            var windows = new List<PlanningWindow>();
            authority.PlanningStarted += windows.Add;

            authority.Start();
            authority.Tick();

            double expected = clock.NowSeconds + harness.Rules.Match.PlanningDurationSeconds - 0.2 - harness.Network.LatencyMarginSeconds;
            Assert.That(authority.RoundTripSeconds, Is.EqualTo(0.2).Within(1e-9));
            Assert.That(windows.Single().DeadlineSeconds, Is.EqualTo(expected).Within(1e-9));
        }

        [Test]
        public void ACommandThePhoneRefuses_IsNeverSent()
        {
            var harness = new ServerHarness();
            var clock = new ManualClock();
            RemoteTurnAuthority authority = StartPlanning(harness, clock, out TestPlayer opponent);
            int before = opponent.Inbox.Count;

            CommandResult notInLoadout = authority.Send(authority.Side, new SelectWeaponCommand(WeaponIds.Sword));
            clock.Advance(harness.Rules.Match.PlanningDurationSeconds - 1.0); // inside the phone's lock-out
            CommandResult lockedOut = authority.Send(authority.Side, new SelectWeaponCommand(WeaponIds.Mace));
            harness.Tick();
            opponent.Read();

            Assert.That(notInLoadout.Rejection, Is.EqualTo(CommandRejection.WeaponNotInLoadout));
            Assert.That(lockedOut.Rejection, Is.EqualTo(CommandRejection.WeaponSwitchLockedOut));
            Assert.That(opponent.Since(before).OfType<PublicStateMessage>(), Is.Empty, "nothing reached the server");
        }

        [Test]
        public void ACommandOnlyTheServerRefuses_PutsThePhonesPlanRight()
        {
            var harness = new ServerHarness();
            var clock = new ManualClock();
            RemoteTurnAuthority authority = StartPlanning(harness, clock, out _);
            var refusals = new List<CommandRejection>();
            var ownStates = new List<PublicPlanningState>();
            authority.CommandRefused += refusals.Add;
            authority.PublicStateChanged += (side, state) =>
            {
                if (side == authority.Side) ownStates.Add(state);
            };

            CommandResult phone = authority.Send(authority.Side, new SelectWeaponCommand(WeaponIds.Mace));
            harness.Clock.Advance(harness.Rules.Match.PlanningDurationSeconds - 1.0); // it reaches the server inside the lock-out
            harness.Tick();
            authority.Tick();

            Assert.That(phone.Accepted, Is.True, "the phone allowed it");
            Assert.That(refusals, Is.EqualTo(new[] { CommandRejection.WeaponSwitchLockedOut }));
            Assert.That(authority.OwnPlan.Weapon, Is.EqualTo(WeaponIds.Rapier), "the phone's copy is the server's plan again");
            Assert.That(ownStates.Select(state => state.Weapon), Is.EqualTo(new[] { WeaponIds.Mace, WeaponIds.Rapier }));
        }

        [Test]
        public void CommandsForTheOtherSide_OrOutsidePlanning_AreRefused()
        {
            var harness = new ServerHarness();
            var clock = new ManualClock();
            var join = new OnlineMatchJoin(new ServerLink(() => harness.Loopback.Connect(), clock, harness.Network), Hello());
            harness.Tick();
            join.Tick();
            RemoteTurnAuthority authority = StartAgainst(harness, join, clock, out _);

            CommandResult beforePlanning = authority.Send(authority.Side, new SetReadyCommand(true));
            authority.Start();
            authority.Tick();
            CommandResult otherSide = authority.Send(authority.Side.Opponent(), new SetReadyCommand(true));

            Assert.That(beforePlanning.Rejection, Is.EqualTo(CommandRejection.NotInPlanningPhase));
            Assert.That(otherSide.Rejection, Is.EqualTo(CommandRejection.NotInPlanningPhase));
        }

        [Test]
        public void ADroppedLink_RejoinsByItself_AndTheMatchGoesOn()
        {
            var run = new OnlineMatchRun(KnockOutTurn);
            bool dropped = false;

            run.Run(everyFrame: frame =>
            {
                if (dropped || run.Left.Results.Count < 2) return;
                run.Left.LastConnection.Close(); // the phone loses its signal mid-match
                dropped = true;
            });

            Assert.That(dropped, Is.True);
            Assert.That(run.Left.Connections, Is.EqualTo(2), "one rejoin, with the token");
            Assert.That(run.Left.Resynchronizations, Is.EqualTo(1), "the phone got the board as it stood");
            Assert.That(run.Right.OpponentNotices.Select(n => n.Status), Has.Member(ConnectionStatus.Dropped).And.Member(ConnectionStatus.Reconnected));
            Assert.That(run.Left.Outcome?.Winner, Is.EqualTo(Side.Left), "the match went on to its end");
            Assert.That(run.Right.Outcome?.Winner, Is.EqualTo(Side.Left));
        }

        [Test]
        public void AfterTheMatch_TheLinkEndsForGood_AndNeverRejoins()
        {
            var run = new OnlineMatchRun(0);
            run.Run();

            for (int frame = 0; frame < 300; frame++)
            {
                run.Left.Frame(OnlineMatchRun.FrameSeconds);
            }

            Assert.That(run.Left.Link.IsEnded, Is.True);
            Assert.That(run.Left.Link.EndReason, Is.EqualTo(GoodbyeReason.MatchOver));
            Assert.That(run.Left.Connections, Is.EqualTo(1));
        }

        [Test]
        public void ADropBeforeAnOpponentIsFound_EndsTheJoin()
        {
            var network = new LoopbackNetwork();
            INetListener listener = network.Listen();
            INetConnection client = null;
            var join = new OnlineMatchJoin(new ServerLink(() => client = network.Connect(), new ManualClock()), Hello());

            client.Close();
            join.Tick();

            Assert.That(join.Stage, Is.EqualTo(OnlineJoinStage.Ended));
            Assert.That(join.Link.EndReason, Is.EqualTo(GoodbyeReason.ConnectionLost));
            listener.Dispose();
        }

        [Test]
        public void AMessageFromANewerServer_EndsTheLink_AsAVersionMismatch()
        {
            var network = new LoopbackNetwork();
            INetListener listener = network.Listen();
            var join = new OnlineMatchJoin(new ServerLink(() => network.Connect(), new ManualClock()), Hello());
            listener.TryAccept(out INetConnection server);
            byte[] newer = ProtocolCodec.Encode(new PlanningLockedMessage(0));
            newer[0] = (byte)(ProtocolCodec.CurrentVersion + 1);

            server.Send(newer);
            join.Tick();

            Assert.That(join.Stage, Is.EqualTo(OnlineJoinStage.Ended));
            Assert.That(join.Link.EndReason, Is.EqualTo(GoodbyeReason.VersionMismatch));
        }

        private static ClientHelloMessage Hello() =>
            new ClientHelloMessage(Handedness.Right, new[] { SignatureMoveIds.FiveFingerExplodingTouch });

        /// <summary>The phone joins (already said hello), a test player is its opponent, both pick: the authority, not started.</summary>
        private static RemoteTurnAuthority StartAgainst(ServerHarness harness, OnlineMatchJoin join, ManualClock clock, out TestPlayer opponent)
        {
            opponent = harness.Join(Handedness.Left);
            join.Tick();
            join.Pick(new[] { WeaponIds.Rapier, WeaponIds.Mace, WeaponIds.Shield });
            opponent.Read();
            opponent.Pick(WeaponIds.Mace, WeaponIds.Spear, WeaponIds.Shield);
            harness.Tick();
            join.Tick();
            opponent.Read();
            Assert.That(join.Stage, Is.EqualTo(OnlineJoinStage.Ready));
            return join.CreateAuthority(harness.Rules, new SimulationSettings(), new RulePolicies(), clock, harness.Network);
        }

        /// <summary>The phone's match, started, its first planning phase open.</summary>
        private static RemoteTurnAuthority StartPlanning(ServerHarness harness, ManualClock clock, out TestPlayer opponent)
        {
            var join = new OnlineMatchJoin(new ServerLink(() => harness.Loopback.Connect(), clock, harness.Network), Hello());
            harness.Tick();
            join.Tick();
            RemoteTurnAuthority authority = StartAgainst(harness, join, clock, out opponent);
            authority.Start();
            authority.Tick();
            return authority;
        }
    }
}
