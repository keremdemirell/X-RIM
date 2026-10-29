using NUnit.Framework;
using XRim.App;
using XRim.Core;
using XRim.Networking.Protocol;

namespace XRim.Tests.EditMode.App
{
    /// <summary>
    /// The connection banner during an online match (Session 19; GDD §3, designer 2026-10-25, Q4): the opponent reconnecting with the
    /// server's wait counting down, then disconnected or away, cleared when they return; this device's own drop said first.
    /// </summary>
    public sealed class OnlineNoticesTests
    {
        [Test]
        public void Nothing_ToSay_SaysNothing()
        {
            var notices = new OnlineNotices();

            Assert.That(notices.Title(0.0), Is.Empty);
            Assert.That(notices.Detail(0.0), Is.Empty);
        }

        [Test]
        public void AnOpponentTheServerWaitsFor_CountsDown_ThenIsDisconnected()
        {
            var notices = new OnlineNotices();
            notices.Opponent(new ConnectionNoticeMessage(Side.Right, ConnectionStatus.Dropped, 15f), 10.0);

            Assert.That(notices.Title(10.0), Is.EqualTo(MatchLabels.OpponentReconnectingTitle));
            Assert.That(notices.Detail(10.0), Is.EqualTo(MatchLabels.OpponentReconnectingDetail(15.0)));
            Assert.That(notices.Detail(17.2), Is.EqualTo(MatchLabels.OpponentReconnectingDetail(7.8)));
            Assert.That(notices.Title(25.5), Is.EqualTo(MatchLabels.OpponentDisconnectedTitle), "the wait is over: the match goes on");
        }

        [Test]
        public void EachStatus_HasItsWords_AndAReturnClearsThem()
        {
            var notices = new OnlineNotices();

            notices.Opponent(new ConnectionNoticeMessage(Side.Right, ConnectionStatus.Dropped, 0f), 0.0);
            Assert.That(notices.Title(0.0), Is.EqualTo(MatchLabels.OpponentDisconnectedTitle));

            notices.Opponent(new ConnectionNoticeMessage(Side.Right, ConnectionStatus.StoppedWaiting, 0f), 1.0);
            Assert.That(notices.Title(1.0), Is.EqualTo(MatchLabels.OpponentGoneTitle));

            notices.Opponent(new ConnectionNoticeMessage(Side.Right, ConnectionStatus.Reconnected, 0f), 2.0);
            Assert.That(notices.Title(2.0), Is.Empty);
        }

        [Test]
        public void ThisDevicesOwnDrop_IsSaidFirst_UntilItRejoins()
        {
            var notices = new OnlineNotices();
            notices.Opponent(new ConnectionNoticeMessage(Side.Right, ConnectionStatus.StoppedWaiting, 0f), 0.0);

            notices.OwnConnection(false);
            Assert.That(notices.Title(0.0), Is.EqualTo(MatchLabels.OnlineReconnectingTitle));

            notices.OwnConnection(true);
            Assert.That(notices.Title(0.0), Is.EqualTo(MatchLabels.OpponentGoneTitle));

            notices.Clear();
            Assert.That(notices.Title(0.0), Is.Empty);
        }
    }
}
