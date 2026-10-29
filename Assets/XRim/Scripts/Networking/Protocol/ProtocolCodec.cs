using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using XRim.Core;

namespace XRim.Networking.Protocol
{
    /// <summary>
    /// Turns a <see cref="NetMessage"/> into bytes and back (Session 19). Engine-free, so the server, the phone and the tests share it.
    /// <para>
    /// A message is a four-byte header, then its body: the protocol version (two bytes, little-endian), the <see cref="MessageKind"/>, and a
    /// flags byte saying whether the body is packed. A body of <see cref="PackFromBytes"/> or more is packed when that makes it smaller: its
    /// unpacked length, then the body deflated. A turn's result shrinks the most, because its frames are mostly small differences.
    /// </para>
    /// <para>
    /// <see cref="Decode(byte[], int, int)"/> refuses anything it cannot trust with a <see cref="ProtocolException"/>: another version, a
    /// message cut short or too large (sent or unpacked), an unknown kind, an impossible value or bytes left over. A transport delivers
    /// whole messages; this class never sees a stream.
    /// </para>
    /// </summary>
    public static class ProtocolCodec
    {
        /// <summary>The protocol this code speaks. Raise it whenever a message's layout changes; the two ends must match.</summary>
        public const ushort CurrentVersion = 1;

        public const int HeaderBytes = 4;

        /// <summary>Bodies this large or larger are tried packed (technical).</summary>
        public const int PackFromBytes = 512;

        private const byte PackedFlag = 1;

        /// <summary>Every event type a turn's result can carry (tests check that every one the game defines is here).</summary>
        public static IReadOnlyList<Type> SupportedEventTypes => EventCodec.SupportedTypes;

        /// <summary>Every planning command a client can send.</summary>
        public static IReadOnlyList<Type> SupportedCommandTypes => CommandCodec.SupportedTypes;

        /// <param name="allowPacking">False sends the body unpacked whatever its size (to measure, or if a platform cannot deflate).</param>
        public static byte[] Encode(NetMessage message, bool allowPacking = true)
        {
            Guard.NotNull(message, nameof(message));
            var body = new PacketWriter();
            MessageBodies.Write(body, message);

            byte flags = 0;
            byte[] payload = body.Buffer;
            int payloadLength = body.Length;
            if (allowPacking && payloadLength >= PackFromBytes)
            {
                byte[] packed = Pack(body.Buffer, body.Length);
                if (packed.Length < payloadLength)
                {
                    payload = packed;
                    payloadLength = packed.Length;
                    flags |= PackedFlag;
                }
            }

            int total = HeaderBytes + payloadLength;
            if (total > ProtocolLimits.MaxMessageBytes)
                throw new ArgumentException($"A {message.Kind} message of {total} bytes is larger than the protocol allows ({ProtocolLimits.MaxMessageBytes}).",
                    nameof(message));

            var bytes = new byte[total];
            bytes[0] = (byte)CurrentVersion;
            bytes[1] = (byte)(CurrentVersion >> 8);
            bytes[2] = (byte)message.Kind;
            bytes[3] = flags;
            Array.Copy(payload, 0, bytes, HeaderBytes, payloadLength);
            return bytes;
        }

        public static NetMessage Decode(byte[] data) => Decode(Guard.NotNull(data, nameof(data)), 0, data.Length);

        public static NetMessage Decode(byte[] data, int offset, int count)
        {
            Guard.NotNull(data, nameof(data));
            if (offset < 0 || count < 0 || offset + count > data.Length) throw new ArgumentOutOfRangeException(nameof(count));
            if (count > ProtocolLimits.MaxMessageBytes)
                throw new ProtocolException(ProtocolError.TooLarge, $"A message of {count} bytes is larger than the protocol allows ({ProtocolLimits.MaxMessageBytes}).");
            if (count < HeaderBytes) throw new ProtocolException(ProtocolError.Truncated, "The message is shorter than its header.");

            int version = data[offset] | (data[offset + 1] << 8);
            if (version != CurrentVersion)
                throw new ProtocolException(ProtocolError.VersionMismatch,
                    $"The message speaks protocol version {version}; this build speaks version {CurrentVersion}.");

            var kind = (MessageKind)data[offset + 2];
            if (!MessageKinds.IsKnown(kind))
                throw new ProtocolException(ProtocolError.UnknownKind, $"A message of an unknown kind ({data[offset + 2]}).");

            byte flags = data[offset + 3];
            if ((flags & ~PackedFlag) != 0) throw new ProtocolException(ProtocolError.Malformed, $"The message's header has unknown flags ({flags}).");

            byte[] body = data;
            int bodyOffset = offset + HeaderBytes;
            int bodyLength = count - HeaderBytes;
            if ((flags & PackedFlag) != 0)
            {
                body = Unpack(data, bodyOffset, bodyLength);
                bodyOffset = 0;
                bodyLength = body.Length;
            }

            var reader = new PacketReader(body, bodyOffset, bodyLength);
            NetMessage message;
            try
            {
                message = MessageBodies.Read(kind, reader);
            }
            catch (ProtocolException)
            {
                throw;
            }
            catch (Exception error) when (error is ArgumentException || error is FormatException || error is NotSupportedException ||
                                          error is InvalidOperationException)
            {
                // A value each field accepts can still make an impossible whole (a clash with no winner, a head on the floor as a club).
                throw new ProtocolException(ProtocolError.Malformed, $"The {kind} message holds an impossible value: {error.Message}", error);
            }

            if (!reader.AtEnd)
                throw new ProtocolException(ProtocolError.Malformed, $"The {kind} message has {reader.RemainingBytes} bytes left over after its contents.");
            return message;
        }

        /// <summary>A packed body: its unpacked length, then the deflated bytes, so the reader can refuse a body that unpacks to anything else.</summary>
        private static byte[] Pack(byte[] buffer, int length)
        {
            var packed = new PacketWriter();
            packed.WriteVarUInt((ulong)length);
            using (var output = new MemoryStream())
            {
                using (var deflate = new DeflateStream(output, CompressionLevel.Optimal, true))
                {
                    deflate.Write(buffer, 0, length);
                }

                packed.WriteBytes(output.GetBuffer(), 0, (int)output.Length);
            }

            return packed.ToArray();
        }

        private static byte[] Unpack(byte[] data, int offset, int count)
        {
            var lengthReader = new PacketReader(data, offset, count);
            ulong declared = lengthReader.ReadVarUInt();
            if (declared > ProtocolLimits.MaxUnpackedBytes)
                throw new ProtocolException(ProtocolError.TooLarge,
                    $"The message says it unpacks to {declared} bytes, more than the protocol allows ({ProtocolLimits.MaxUnpackedBytes}).");
            int lengthBytes = count - lengthReader.RemainingBytes;

            try
            {
                using (var input = new MemoryStream(data, offset + lengthBytes, count - lengthBytes, false))
                using (var deflate = new DeflateStream(input, CompressionMode.Decompress))
                {
                    var body = new byte[(int)declared];
                    int filled = 0;
                    int read;
                    while (filled < body.Length && (read = deflate.Read(body, filled, body.Length - filled)) > 0)
                    {
                        filled += read;
                    }

                    if (filled < body.Length)
                        throw new ProtocolException(ProtocolError.Truncated, $"The packed body unpacks to {filled} bytes, not the {declared} it declares.");
                    if (deflate.Read(new byte[1], 0, 1) > 0)
                        throw new ProtocolException(ProtocolError.Malformed, $"The packed body unpacks to more than the {declared} bytes it declares.");
                    return body;
                }
            }
            catch (Exception error) when (error is InvalidDataException || error is IOException)
            {
                // .NET reports damaged deflate data as InvalidDataException; Unity's Mono (native zlib) as a plain IOException.
                throw new ProtocolException(ProtocolError.Malformed, "The message's packed body is damaged.", error);
            }
        }
    }
}
