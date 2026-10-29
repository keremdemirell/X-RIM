using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using XRim.Networking.Protocol;

namespace XRim.Networking.Transport
{
    /// <summary>
    /// A connection over TCP (Session 19; designer 2026-10-25: plain TCP behind the transport interface), built on .NET sockets alone, so
    /// it runs on the server, on a phone and in the check tool's tests. The socket never blocks: every call does what it can at once and
    /// <see cref="TryReceive"/> and <see cref="Send"/> move bytes as they go, from the caller's own frame. Each message travels as its length
    /// (four bytes, little-endian) and then its bytes; a length the protocol does not allow closes the connection. Messages are sent at once
    /// (no Nagle delay). Encryption (TLS) comes before release (Session 22).
    /// </summary>
    public sealed class TcpNetConnection : INetConnection
    {
        private const int LengthPrefixBytes = 4;
        private const int ReadChunkBytes = 64 * 1024;

        private readonly Socket _socket;
        private readonly Queue<byte[]> _arrived = new Queue<byte[]>();
        private byte[] _incoming = new byte[ReadChunkBytes];
        private int _incomingLength;
        private byte[] _outgoing = new byte[ReadChunkBytes];
        private int _outgoingStart;
        private int _outgoingLength;
        private bool _connecting;
        private bool _closed;

        internal TcpNetConnection(Socket socket, bool connecting)
        {
            _socket = socket;
            _socket.Blocking = false;
            _connecting = connecting;
            SendAtOnce(socket);
        }

        /// <summary>
        /// Starts connecting to a server; the connection is usable at once (messages wait until it is up) and reads as closed if it fails.
        /// A name other than a numeric address is looked up first, which can take a moment; a name that cannot be found throws
        /// <see cref="SocketException"/> (the caller says so and tries again).
        /// </summary>
        public static TcpNetConnection Connect(string host, int port)
        {
            if (!IPAddress.TryParse(host, out IPAddress address)) address = Resolve(host);
            var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            socket.Blocking = false;
            SendAtOnce(socket);
            bool connecting = false;
            try
            {
                socket.Connect(new IPEndPoint(address, port));
            }
            catch (SocketException error) when (error.SocketErrorCode == SocketError.WouldBlock || error.SocketErrorCode == SocketError.InProgress ||
                                                 error.SocketErrorCode == SocketError.AlreadyInProgress)
            {
                connecting = true;
            }
            catch (Exception error) when (IsSocketFault(error))
            {
                // Refused at once (some runtimes say so here rather than later): a connection that reads as closed, like a later failure.
                var failed = new TcpNetConnection(socket, true);
                failed.Shut();
                return failed;
            }

            return new TcpNetConnection(socket, connecting);
        }

        public bool IsOpen => !_closed;

        public void Send(byte[] message)
        {
            if (message == null) throw new ArgumentNullException(nameof(message));
            if (_closed) return;
            Reserve(ref _outgoing, _outgoingStart + _outgoingLength + LengthPrefixBytes + message.Length);
            int at = _outgoingStart + _outgoingLength;
            WriteLength(_outgoing, at, message.Length);
            Array.Copy(message, 0, _outgoing, at + LengthPrefixBytes, message.Length);
            _outgoingLength += LengthPrefixBytes + message.Length;
            Pump();
        }

        public bool TryReceive(out byte[] message)
        {
            Pump();
            if (_arrived.Count == 0)
            {
                message = null;
                return false;
            }

            message = _arrived.Dequeue();
            return true;
        }

        public void Close()
        {
            if (_closed) return;
            Flush();
            Shut();
        }

        private void Pump()
        {
            if (_closed) return;
            if (_connecting && !FinishConnecting()) return;
            Flush();
            if (!_closed) Read();
        }

        private bool FinishConnecting()
        {
            try
            {
                if (_socket.Poll(0, SelectMode.SelectError))
                {
                    Shut();
                    return false;
                }

                if (!_socket.Poll(0, SelectMode.SelectWrite)) return false;
                _connecting = false;
                return true;
            }
            catch (Exception error) when (IsSocketFault(error))
            {
                Shut();
                return false;
            }
        }

        private void Flush()
        {
            if (_connecting) return;
            try
            {
                while (_outgoingLength > 0)
                {
                    int sent = _socket.Send(_outgoing, _outgoingStart, _outgoingLength, SocketFlags.None, out SocketError error);
                    if (error == SocketError.WouldBlock) return;
                    if (error != SocketError.Success || sent <= 0)
                    {
                        Shut();
                        return;
                    }

                    _outgoingStart += sent;
                    _outgoingLength -= sent;
                }

                _outgoingStart = 0;
            }
            catch (Exception error) when (IsSocketFault(error))
            {
                Shut();
            }
        }

        private void Read()
        {
            while (true)
            {
                Reserve(ref _incoming, _incomingLength + ReadChunkBytes);
                int read;
                SocketError error;
                try
                {
                    read = _socket.Receive(_incoming, _incomingLength, ReadChunkBytes, SocketFlags.None, out error);
                }
                catch (Exception fault) when (IsSocketFault(fault))
                {
                    read = 0;
                    error = SocketError.SocketError;
                }

                if (error == SocketError.WouldBlock) break;
                if (error != SocketError.Success || read <= 0)
                {
                    // The other end closed (or the link failed): what already arrived can still be read.
                    TakeWholeMessages();
                    Shut();
                    return;
                }

                _incomingLength += read;
                if (!TakeWholeMessages()) return;
            }
        }

        /// <summary>Moves every complete message out of the incoming bytes; false (and closed) when a length is impossible.</summary>
        private bool TakeWholeMessages()
        {
            int at = 0;
            while (_incomingLength - at >= LengthPrefixBytes)
            {
                int length = ReadLength(_incoming, at);
                if (length < 0 || length > ProtocolLimits.MaxMessageBytes)
                {
                    Shut();
                    return false;
                }

                if (_incomingLength - at - LengthPrefixBytes < length) break;
                var message = new byte[length];
                Array.Copy(_incoming, at + LengthPrefixBytes, message, 0, length);
                _arrived.Enqueue(message);
                at += LengthPrefixBytes + length;
            }

            if (at > 0)
            {
                Array.Copy(_incoming, at, _incoming, 0, _incomingLength - at);
                _incomingLength -= at;
            }

            return true;
        }

        private void Shut()
        {
            _closed = true;
            try
            {
                if (!_connecting) _socket.Shutdown(SocketShutdown.Both);
            }
            catch (Exception error) when (IsSocketFault(error))
            {
                // Already gone.
            }

            _socket.Close();
        }

        /// <summary>
        /// No Nagle delay: a small message goes at once. Only a speed-up, so a socket that refuses it (once its connection has failed,
        /// Windows refuses) keeps going without it; the failure itself shows as a closed connection.
        /// </summary>
        private static void SendAtOnce(Socket socket)
        {
            try
            {
                socket.NoDelay = true;
            }
            catch (Exception error) when (IsSocketFault(error))
            {
                // Not available on this socket.
            }
        }

        /// <summary>
        /// A fault of the socket layer, which closes the connection rather than stopping the frame. .NET and Unity's Mono throw different
        /// types for the same fault (Mono: an <see cref="ArgumentException"/> for an option on a failed socket), so all of them count.
        /// </summary>
        public static bool IsSocketFault(Exception error) =>
            error is SocketException || error is ObjectDisposedException || error is ArgumentException || error is InvalidOperationException ||
            error is NotSupportedException;

        private static IPAddress Resolve(string host)
        {
            IPAddress[] addresses = Dns.GetHostAddresses(host);
            foreach (IPAddress candidate in addresses)
            {
                if (candidate.AddressFamily == AddressFamily.InterNetwork) return candidate;
            }

            if (addresses.Length == 0) throw new SocketException((int)SocketError.HostNotFound);
            return addresses[0];
        }

        private static void Reserve(ref byte[] buffer, int needed)
        {
            if (needed <= buffer.Length) return;
            int capacity = buffer.Length;
            while (capacity < needed) capacity *= 2;
            Array.Resize(ref buffer, capacity);
        }

        private static void WriteLength(byte[] buffer, int at, int length)
        {
            buffer[at] = (byte)length;
            buffer[at + 1] = (byte)(length >> 8);
            buffer[at + 2] = (byte)(length >> 16);
            buffer[at + 3] = (byte)(length >> 24);
        }

        private static int ReadLength(byte[] buffer, int at) =>
            buffer[at] | (buffer[at + 1] << 8) | (buffer[at + 2] << 16) | (buffer[at + 3] << 24);
    }
}
