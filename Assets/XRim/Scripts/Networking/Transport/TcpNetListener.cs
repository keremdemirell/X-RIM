using System;
using System.Net;
using System.Net.Sockets;

namespace XRim.Networking.Transport
{
    /// <summary>
    /// Where a server takes TCP connections (Session 19), never blocking: <see cref="TryAccept"/> takes a waiting connection or returns at
    /// once. Port 0 picks a free port (tests); <see cref="Port"/> says which.
    /// </summary>
    public sealed class TcpNetListener : INetListener
    {
        private const int Backlog = 32;

        private readonly Socket _socket;
        private bool _disposed;

        /// <param name="address"><see cref="IPAddress.Any"/> takes players from anywhere; <see cref="IPAddress.Loopback"/> only from this machine.</param>
        public TcpNetListener(IPAddress address, int port)
        {
            _socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp) { Blocking = false };
            _socket.Bind(new IPEndPoint(address, port));
            _socket.Listen(Backlog);
        }

        public int Port => ((IPEndPoint)_socket.LocalEndPoint).Port;

        public bool TryAccept(out INetConnection connection)
        {
            connection = null;
            if (_disposed) return false;
            try
            {
                if (!_socket.Poll(0, SelectMode.SelectRead)) return false;
                connection = new TcpNetConnection(_socket.Accept(), false);
                return true;
            }
            catch (Exception error) when (TcpNetConnection.IsSocketFault(error))
            {
                return false;
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _socket.Close();
        }
    }
}
