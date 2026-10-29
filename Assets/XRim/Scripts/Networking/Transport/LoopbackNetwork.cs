using System;
using System.Collections.Generic;

namespace XRim.Networking.Transport
{
    /// <summary>
    /// A network inside one process (Session 19): a server listens, clients connect, and messages pass between the two ends at once, in
    /// order, untouched. Tests use it to run a server and its players without sockets; a connection can be cut from either end to play
    /// out a dropped connection.
    /// </summary>
    public sealed class LoopbackNetwork
    {
        private Listener _listener;

        /// <summary>Starts listening; a second listener replaces the first.</summary>
        public INetListener Listen()
        {
            _listener = new Listener(this);
            return _listener;
        }

        /// <summary>A new connection to the listening server; it arrives closed when nothing listens.</summary>
        public INetConnection Connect()
        {
            var client = new End();
            var server = new End();
            client.Peer = server;
            server.Peer = client;
            if (_listener == null || _listener.IsDisposed)
            {
                client.Close();
                return client;
            }

            _listener.Pending.Enqueue(server);
            return client;
        }

        private void Forget(Listener listener)
        {
            if (_listener == listener) _listener = null;
        }

        private sealed class Listener : INetListener
        {
            private readonly LoopbackNetwork _network;

            public Listener(LoopbackNetwork network)
            {
                _network = network;
            }

            public Queue<End> Pending { get; } = new Queue<End>();

            public bool IsDisposed { get; private set; }

            public bool TryAccept(out INetConnection connection)
            {
                connection = null;
                if (IsDisposed || Pending.Count == 0) return false;
                connection = Pending.Dequeue();
                return true;
            }

            public void Dispose()
            {
                IsDisposed = true;
                while (Pending.Count > 0) Pending.Dequeue().Close();
                _network.Forget(this);
            }
        }

        private sealed class End : INetConnection
        {
            private readonly Queue<byte[]> _inbox = new Queue<byte[]>();
            private bool _closed;

            public End Peer { get; set; }

            public bool IsOpen => !_closed;

            public void Send(byte[] message)
            {
                if (message == null) throw new ArgumentNullException(nameof(message));
                if (_closed) return;
                Peer._inbox.Enqueue((byte[])message.Clone());
            }

            public bool TryReceive(out byte[] message)
            {
                if (_inbox.Count == 0)
                {
                    message = null;
                    return false;
                }

                message = _inbox.Dequeue();
                return true;
            }

            public void Close()
            {
                _closed = true;
                if (Peer != null) Peer._closed = true;
            }
        }
    }
}
