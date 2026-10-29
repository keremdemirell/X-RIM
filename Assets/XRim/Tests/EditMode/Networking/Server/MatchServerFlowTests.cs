using System.Linq;
using NUnit.Framework;
using XRim.Core;
using XRim.Networking.Protocol;
using XRim.Rules;
using XRim.Rules.Match;
using XRim.Rules.Planning;

namespace XRim.Tests.EditMode.Networking.Server
{
    /// <summary>
    /// Two players play a whole match through the server (Session 19): they are paired and told their sides and the server's settings, pick
    /// their loadouts blind, then plan, watch and report each turn until the match ends; the next planning phase waits for both playbacks.
    /// </summary>
    public sealed class MatchServerFlowTests
    {
        [Test]
        public void TwoPlayers_ArePaired_EachToldTheirSide_TheirToken_AndTheServersSettings()
        {
            var harness = new ServerHarness();
            TestPlayer first = harness.Join();
            TestPlayer second = harness.Join();
            ServerHarness.Read(first, second);

            MatchFoundMessage left = first.Last<MatchFoundMessage>();
            MatchFoundMessage right = second.Last<MatchFoundMessage>();
            Assert.That(left.YourSide, Is.EqualTo(Side.Left));
            Assert.That(right.YourSide, Is.EqualTo(Side.Right));
            Assert.That(left.ReconnectToken, Is.Not.Empty.And.Not.EqualTo(right.ReconnectToken));
            Assert.That(left.SettingsText, Is.EqualTo(ServerHarness.SettingsText));
            Assert.That(harness.Server.MatchCount, Is.EqualTo(1));
        }

        [Test]
        public void Loadouts_StayHiddenUntilBothPicked_ThenBothAreRevealed()
        {
            var harness = new ServerHarness();
            TestPlayer left = harness.Join(Handedness.Right);
            TestPlayer right = harness.Join(Handedness.Left);

            left.Pick(WeaponIds.Rapier, WeaponIds.Mace, WeaponIds.Shield);
            harness.Tick();
            ServerHarness.Read(left, right);
            Assert.That(right.All<MatchStartedMessage>(), Is.Empty, "nothing about the first pick reaches the other player");

            right.Pick(WeaponIds.Spear, WeaponIds.Sword, WeaponIds.Shield);
            harness.Tick();
            ServerHarness.Read(left, right);

            foreach (TestPlayer player in new[] { left, right })
            {
                MatchSetup setup = player.Last<MatchStartedMessage>().Setup;
                Assert.That(setup.Fighters.Left.Loadout, Is.EqualTo(new[] { WeaponIds.Rapier, WeaponIds.Mace, WeaponIds.Shield }));
                Assert.That(setup.Fighters.Right.Loadout, Is.EqualTo(new[] { WeaponIds.Spear, WeaponIds.Sword, WeaponIds.Shield }));
                Assert.That(setup.Fighters.Right.Handedness, Is.EqualTo(Handedness.Left));
                Assert.That(player.Last<PlanningStartedMessage>().TurnIndex, Is.EqualTo(0));
            }
        }

        [Test]
        public void AFullMatch_IsPlayedThroughTheServer()
        {
            var harness = new ServerHarness(script: (turn, state) =>
            {
                if (turn == 2) state.Fighters.Right.Hp = 0f;
            });
            (TestPlayer left, TestPlayer right) = harness.StartMatch();

            for (int turn = 0; turn < 3; turn++)
            {
                harness.PlayTurn(left, right);
            }

            foreach (TestPlayer player in new[] { left, right })
            {
                Assert.That(player.All<TurnResolvedMessage>().Select(m => m.Result.TurnIndex), Is.EqualTo(new[] { 0, 1, 2 }));
                MatchOutcome outcome = player.Last<MatchEndedMessage>().Outcome;
                Assert.That(outcome.Winner, Is.EqualTo(Side.Left));
                Assert.That(outcome.Reason, Is.EqualTo(MatchEndReason.KnockOut));
                Assert.That(player.Last<GoodbyeMessage>().Reason, Is.EqualTo(GoodbyeReason.MatchOver));
                Assert.That(player.IsOpen, Is.False);
            }

            Assert.That(harness.Simulator.Inputs.Count, Is.EqualTo(3), "the server simulated every turn itself");
            Assert.That(harness.Server.MatchCount, Is.EqualTo(0));
        }

        [Test]
        public void TheNextPlanning_OpensOnlyWhenBothPlayersFinishedPlayback()
        {
            var harness = new ServerHarness();
            (TestPlayer left, TestPlayer right) = harness.StartMatch();
            left.Command(new SetReadyCommand(true));
            right.Command(new SetReadyCommand(true));
            harness.Tick();
            ServerHarness.Read(left, right);

            left.ReportPlayback();
            harness.Advance(5.0);
            ServerHarness.Read(left, right);
            Assert.That(right.CurrentTurn, Is.EqualTo(0), "one player still watching holds the next turn");

            right.ReportPlayback();
            harness.Tick();
            ServerHarness.Read(left, right);
            Assert.That(left.CurrentTurn, Is.EqualTo(1));
            Assert.That(right.CurrentTurn, Is.EqualTo(1));
        }

        [Test]
        public void TheNextPlanning_OpensAnyway_WhenThePlaybackWaitRunsOut()
        {
            var harness = new ServerHarness();
            (TestPlayer left, TestPlayer right) = harness.StartMatch();
            left.Command(new SetReadyCommand(true));
            right.Command(new SetReadyCommand(true));
            harness.Tick();

            harness.Advance(harness.Network.PlaybackWaitSeconds - 0.5);
            ServerHarness.Read(left, right);
            Assert.That(left.CurrentTurn, Is.EqualTo(0));

            harness.Advance(0.5);
            ServerHarness.Read(left, right);
            Assert.That(left.CurrentTurn, Is.EqualTo(1));
        }

        [Test]
        public void AThirdPlayer_WaitsForAFourth()
        {
            var harness = new ServerHarness();
            harness.Join();
            harness.Join();
            TestPlayer third = harness.Join();
            third.Read();

            Assert.That(harness.Server.MatchCount, Is.EqualTo(1));
            Assert.That(harness.Server.WaitingPlayers, Is.EqualTo(1));
            Assert.That(third.All<MatchFoundMessage>(), Is.Empty);
        }

        [Test]
        public void APing_IsAnswered_WithTheServersClock()
        {
            var harness = new ServerHarness();
            (TestPlayer left, _) = harness.StartMatch();
            harness.Clock.Advance(3.25);

            left.Send(new PingMessage(7, 41.5));
            harness.Tick();
            left.Read();

            PongMessage pong = left.Last<PongMessage>();
            Assert.That(pong.PingId, Is.EqualTo(7));
            Assert.That(pong.ClientSentSeconds, Is.EqualTo(41.5));
            Assert.That(pong.ServerNowSeconds, Is.EqualTo(3.25));
        }
    }
}
