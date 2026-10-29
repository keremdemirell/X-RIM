using System.Linq;
using NUnit.Framework;
using XRim.Core;
using XRim.Networking;
using XRim.Networking.Protocol;
using XRim.Rules;
using XRim.Rules.Match;
using XRim.Rules.Planning;

namespace XRim.Tests.EditMode.Networking.Server
{
    /// <summary>
    /// Disconnects (GDD §3, TBD; designer 2026-10-25, flagged defaults): the match goes on; a returning player gets the current board and
    /// phase with their own plan; between turns the server waits for a missing player once per disconnect; turns locked without them count
    /// as idle (three forfeit) unless switched off; a silent connection counts as dropped (before a match, it is told it timed out); a
    /// deserted match is abandoned.
    /// </summary>
    public sealed class ServerDisconnectTests
    {
        [Test]
        public void ADroppedPlayer_RejoinsWithTheirToken_AndGetsTheBoardThePhaseAndTheirOwnPlan()
        {
            var harness = new ServerHarness();
            (TestPlayer left, TestPlayer right) = harness.StartMatch();
            left.Command(new SelectWeaponCommand(WeaponIds.Mace));
            left.Command(new SetBodyMoveCommand(BodyMove.Crouch));
            harness.Advance(4.0);

            left.Drop();
            harness.Tick();
            right.Read();
            Assert.That(right.Last<ConnectionNoticeMessage>().Status, Is.EqualTo(ConnectionStatus.Dropped));

            harness.Advance(2.0);
            int before = left.Inbox.Count;
            left.Rejoin(harness.Loopback.Connect());
            harness.Tick();
            ServerHarness.Read(left, right);

            var rejoined = left.Since(before);
            Assert.That(rejoined.Select(m => m.Kind), Is.EqualTo(new[]
            {
                MessageKind.MatchFound, MessageKind.MatchStarted, MessageKind.PlanningStarted, MessageKind.PlanState,
            }));
            Assert.That(left.Last<MatchFoundMessage>().YourSide, Is.EqualTo(Side.Left));
            PlanningStartedMessage planning = left.Last<PlanningStartedMessage>();
            Assert.That(planning.TurnIndex, Is.EqualTo(0));
            Assert.That(planning.RemainingSeconds, Is.EqualTo(harness.Rules.Match.PlanningDurationSeconds - 6.0).Within(1e-6));
            PlanStateMessage plan = left.Last<PlanStateMessage>();
            Assert.That(plan.Weapon, Is.EqualTo(WeaponIds.Mace));
            Assert.That(plan.BodyMove, Is.EqualTo(BodyMove.Crouch));
            Assert.That(right.Last<ConnectionNoticeMessage>().Status, Is.EqualTo(ConnectionStatus.Reconnected));
        }

        [Test]
        public void AWrongToken_IsRefused()
        {
            var harness = new ServerHarness();
            harness.StartMatch();
            TestPlayer stranger = harness.Connect();

            stranger.Hello(token: "not-a-token");
            harness.Tick();
            stranger.Read();

            Assert.That(stranger.Last<GoodbyeMessage>().Reason, Is.EqualTo(GoodbyeReason.ReconnectRefused));
        }

        [Test]
        public void BetweenTurns_TheServerWaitsForAMissingPlayer_OncePerDisconnect()
        {
            var harness = new ServerHarness();
            (TestPlayer left, TestPlayer right) = harness.StartMatch();
            float wait = harness.Network.ReconnectWaitSeconds;
            float planning = harness.Rules.Match.PlanningDurationSeconds;
            left.Drop();
            right.Command(new SetReadyCommand(true));
            harness.Tick();
            harness.Advance(planning);
            right.Read();
            right.ReportPlayback();
            harness.Tick();

            harness.Advance(wait - 1.0);
            right.Read();
            Assert.That(right.CurrentTurn, Is.EqualTo(0), "the next planning phase waits for the missing player");
            Assert.That(right.All<ConnectionNoticeMessage>().Any(n => n.Status == ConnectionStatus.Dropped && n.WaitSecondsLeft == wait), Is.True,
                "the opponent is told how long the server waits");

            harness.Advance(1.0);
            right.Read();
            Assert.That(right.Last<ConnectionNoticeMessage>().Status, Is.EqualTo(ConnectionStatus.StoppedWaiting));
            Assert.That(right.CurrentTurn, Is.EqualTo(1));

            right.Command(new SetReadyCommand(true));
            harness.Tick();
            harness.Advance(planning);
            right.Read();
            right.ReportPlayback();
            harness.Tick();
            right.Read();
            Assert.That(right.CurrentTurn, Is.EqualTo(2), "no second wait for the same disconnect");
        }

        [Test]
        public void MissedTurns_CountAsIdle_SoThreeForfeit()
        {
            var harness = new ServerHarness();
            (TestPlayer left, TestPlayer right) = harness.StartMatch();
            left.Drop();

            PlayAloneUntilOver(harness, right, 6);

            MatchOutcome outcome = right.Last<MatchEndedMessage>().Outcome;
            Assert.That(outcome.Winner, Is.EqualTo(Side.Right));
            Assert.That(outcome.Reason, Is.EqualTo(MatchEndReason.Forfeit));
            Assert.That(outcome.TurnCount, Is.EqualTo(RuleConstants.IdleTurnsBeforeForfeit));
        }

        [Test]
        public void WithMissedTurnsNotCounted_AMissingPlayerDoesNotForfeit()
        {
            var harness = new ServerHarness(new NetworkSettings { ConnectionTimeoutSeconds = ServerHarness.NoTimeoutSeconds, MissedTurnsCountAsIdle = false });
            (TestPlayer left, TestPlayer right) = harness.StartMatch();
            left.Drop();

            PlayAloneUntilOver(harness, right, RuleConstants.IdleTurnsBeforeForfeit + 1);

            Assert.That(right.All<MatchEndedMessage>(), Is.Empty);
            Assert.That(right.CurrentTurn, Is.EqualTo(RuleConstants.IdleTurnsBeforeForfeit + 1));
        }

        [Test]
        public void ASilentPlayer_CountsAsDropped()
        {
            var harness = new ServerHarness(new NetworkSettings { ConnectionTimeoutSeconds = 10f });
            (TestPlayer left, TestPlayer right) = harness.StartMatch();

            for (int second = 0; second < 11; second++)
            {
                right.Send(new PingMessage(second, second));
                harness.Advance(1.0);
            }

            right.Read();
            Assert.That(right.Last<ConnectionNoticeMessage>().Side, Is.EqualTo(Side.Left));
            Assert.That(right.Last<ConnectionNoticeMessage>().Status, Is.EqualTo(ConnectionStatus.Dropped));
            Assert.That(left.IsOpen, Is.False);
        }

        [Test]
        public void ASilentWaitingPlayer_IsToldItTimedOut()
        {
            var harness = new ServerHarness(new NetworkSettings { ConnectionTimeoutSeconds = 10f });
            TestPlayer waiting = harness.Join();

            harness.Advance(11.0);
            waiting.Read();

            Assert.That(waiting.Last<GoodbyeMessage>().Reason, Is.EqualTo(GoodbyeReason.TimedOut));
            Assert.That(harness.Server.WaitingPlayers, Is.EqualTo(0));
        }

        [Test]
        public void AWaitingPlayerWhoKeepsPinging_WaitsAsLongAsItTakes()
        {
            var harness = new ServerHarness(new NetworkSettings { ConnectionTimeoutSeconds = 10f });
            TestPlayer waiting = harness.Join();

            for (int second = 0; second < 15; second++)
            {
                waiting.Send(new PingMessage(second, second));
                harness.Advance(1.0);
            }

            waiting.Read();
            Assert.That(waiting.All<GoodbyeMessage>(), Is.Empty);
            Assert.That(harness.Server.WaitingPlayers, Is.EqualTo(1));
        }

        [Test]
        public void AConnectionThatNeverSaysHello_IsToldItTimedOut()
        {
            var harness = new ServerHarness(new NetworkSettings { ConnectionTimeoutSeconds = 10f });
            TestPlayer player = harness.Connect();
            harness.Tick();

            harness.Advance(11.0);
            player.Read();

            Assert.That(player.Last<GoodbyeMessage>().Reason, Is.EqualTo(GoodbyeReason.TimedOut));
        }

        [Test]
        public void BothPlayersGone_AbandonTheMatch()
        {
            var harness = new ServerHarness();
            (TestPlayer left, TestPlayer right) = harness.StartMatch();
            left.Drop();
            right.Drop();
            harness.Tick();

            harness.Advance(harness.Network.ReconnectWaitSeconds);

            Assert.That(harness.Server.MatchCount, Is.EqualTo(0));
        }

        [Test]
        public void APlayerLeavingBeforeTheMatchStarts_EndsIt_AndTheOtherIsTold()
        {
            var harness = new ServerHarness();
            TestPlayer left = harness.Join();
            TestPlayer right = harness.Join();
            left.Drop();
            harness.Tick();

            harness.Advance(harness.Network.ReconnectWaitSeconds);
            right.Read();

            Assert.That(right.Last<GoodbyeMessage>().Reason, Is.EqualTo(GoodbyeReason.MatchOver));
            Assert.That(harness.Server.MatchCount, Is.EqualTo(0));
        }

        /// <summary>
        /// The connected player plays a body move every turn (never idle) and watches each turn while the other is gone: each turn runs to
        /// its deadline, then the clock moves a second at a time until the next planning phase opens (after the server's one wait).
        /// </summary>
        private static void PlayAloneUntilOver(ServerHarness harness, TestPlayer player, int turns)
        {
            const int maxWaitSteps = 120;
            for (int turn = 0; turn < turns && !IsOver(player); turn++)
            {
                int planning = player.CurrentTurn;
                player.Command(new SetBodyMoveCommand(BodyMove.Crouch));
                player.Command(new SetReadyCommand(true));
                harness.Tick();
                harness.Advance(harness.Rules.Match.PlanningDurationSeconds);
                player.Read();
                if (IsOver(player)) break;

                player.ReportPlayback();
                harness.Tick();
                player.Read();
                for (int step = 0; step < maxWaitSteps && player.CurrentTurn == planning && !IsOver(player); step++)
                {
                    harness.Advance(1.0);
                    player.Read();
                }
            }
        }

        private static bool IsOver(TestPlayer player) => player.All<MatchEndedMessage>().Count > 0;
    }
}
