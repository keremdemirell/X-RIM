using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using NUnit.Framework;
using XRim.Core;
using XRim.Networking.Protocol;
using XRim.Networking.Transport;
using XRim.Rules.Match;

namespace XRim.Tests.EditMode.Networking.Client
{
    /// <summary>
    /// The TCP transport (Session 19; designer 2026-10-25: plain TCP) on real sockets, on this machine's loopback address only: whole
    /// messages arrive in order whatever their size, a closed end is seen as closed after its last messages, an impossible length closes the
    /// connection, a connection to nothing reads as closed, and a whole online match is played over it.
    /// </summary>
    public sealed class TcpTransportTests
    {
        /// <summary>How many polls a test waits for the operating system to move bytes (each poll sleeps a millisecond).</summary>
        private const int MaxPolls = 5000;

        /// <summary>A three-turn match takes a few dozen frames; this many means it is stuck.</summary>
        private const int MaxFramesOverTcp = 3000;

        [Test]
        public void Messages_CrossARealSocket_WholeAndInOrder_BothWays()
        {
            using (var listener = new TcpNetListener(IPAddress.Loopback, 0))
            {
                TcpNetConnection client = TcpNetConnection.Connect("127.0.0.1", listener.Port);
                INetConnection server = Accept(listener);
                var random = new Random(7);
                List<byte[]> sent = Enumerable.Range(0, 40)
                    .Select(i => Enumerable.Range(0, i == 13 ? 300_000 : random.Next(0, 3000)).Select(_ => (byte)random.Next(256)).ToArray()).ToList();

                foreach (byte[] message in sent)
                {
                    client.Send(message);
                    server.Send(message);
                }

                List<byte[]> atServer = ReceiveCount(server, sent.Count);
                List<byte[]> atClient = ReceiveCount(client, sent.Count);

                Assert.That(atServer, Is.EqualTo(sent));
                Assert.That(atClient, Is.EqualTo(sent));
                client.Close();
                server.Close();
            }
        }

        [Test]
        public void AClosedEnd_IsSeenAsClosed_AfterItsLastMessages()
        {
            using (var listener = new TcpNetListener(IPAddress.Loopback, 0))
            {
                TcpNetConnection client = TcpNetConnection.Connect("localhost", listener.Port);
                INetConnection server = Accept(listener);

                client.Send(new byte[] { 1 });
                client.Send(new byte[] { 2, 2 });
                client.Close();
                List<byte[]> last = ReceiveCount(server, 2);
                bool closed = Poll(() =>
                {
                    server.TryReceive(out _);
                    return !server.IsOpen;
                });

                Assert.That(last.Select(m => m.Length), Is.EqualTo(new[] { 1, 2 }));
                Assert.That(closed, Is.True);
            }
        }

        [Test]
        public void AnImpossibleLength_ClosesTheConnection()
        {
            using (var listener = new TcpNetListener(IPAddress.Loopback, 0))
            using (var raw = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
            {
                raw.Connect(new IPEndPoint(IPAddress.Loopback, listener.Port));
                INetConnection server = Accept(listener);

                raw.Send(BitConverter.GetBytes(ProtocolLimits.MaxMessageBytes + 1));
                bool closed = Poll(() =>
                {
                    server.TryReceive(out _);
                    return !server.IsOpen;
                });

                Assert.That(closed, Is.True);
            }
        }

        [Test]
        public void AConnectionToNothing_ReadsAsClosed()
        {
            int port;
            using (var listener = new TcpNetListener(IPAddress.Loopback, 0))
            {
                port = listener.Port; // free now that it is closed
            }

            TcpNetConnection client = TcpNetConnection.Connect("127.0.0.1", port);
            bool closed = Poll(() =>
            {
                client.TryReceive(out _);
                return !client.IsOpen;
            });

            Assert.That(closed, Is.True);
        }

        [Test]
        public void AWholeOnlineMatch_IsPlayedOverRealTcp()
        {
            var listener = new TcpNetListener(IPAddress.Loopback, 0);
            int port = listener.Port;
            var run = new OnlineMatchRun(2, listener, () => TcpNetConnection.Connect("127.0.0.1", port));

            // A millisecond of real time a frame lets the operating system carry the bytes, as a phone's frame would.
            run.Run(MaxFramesOverTcp, _ => Thread.Sleep(1));

            foreach (OnlineBotClient client in new[] { run.Left, run.Right })
            {
                Assert.That(client.Outcome?.Winner, Is.EqualTo(Side.Left));
                Assert.That(client.Outcome?.Reason, Is.EqualTo(MatchEndReason.KnockOut));
                Assert.That(client.Results.Count, Is.EqualTo(3));
            }

            run.Server.Dispose();
        }

        private static INetConnection Accept(TcpNetListener listener)
        {
            INetConnection accepted = null;
            Assert.That(Poll(() => listener.TryAccept(out accepted)), Is.True, "the connection arrived");
            return accepted;
        }

        private static List<byte[]> ReceiveCount(INetConnection connection, int count)
        {
            var received = new List<byte[]>();
            Poll(() =>
            {
                while (connection.TryReceive(out byte[] message)) received.Add(message);
                return received.Count >= count;
            });
            return received;
        }

        private static bool Poll(Func<bool> done)
        {
            for (int i = 0; i < MaxPolls; i++)
            {
                if (done()) return true;
                Thread.Sleep(1);
            }

            return done();
        }
    }
}
