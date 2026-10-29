using System;
using System.Text;

namespace XRim.Networking.Protocol
{
    /// <summary>
    /// Reads what <see cref="PacketWriter"/> wrote, checking every step: running past the end is <see cref="ProtocolError.Truncated"/>, a
    /// list or text beyond its limit <see cref="ProtocolError.TooLarge"/>, a value no field can hold <see cref="ProtocolError.Malformed"/>.
    /// A list's length is checked against the bytes left before anything is allocated for it.
    /// </summary>
    internal sealed class PacketReader
    {
        /// <summary>A 64-bit integer never needs more than ten 7-bit groups.</summary>
        private const int MaxVarIntBytes = 10;

        private readonly byte[] _data;
        private readonly int _end;
        private int _position;

        public PacketReader(byte[] data, int offset, int count)
        {
            _data = data;
            _position = offset;
            _end = offset + count;
        }

        public bool AtEnd => _position >= _end;

        public int RemainingBytes => _end - _position;

        public byte ReadByte()
        {
            if (_position >= _end) throw Truncated();
            return _data[_position++];
        }

        public bool ReadBool(string what)
        {
            byte value = ReadByte();
            if (value > 1) throw Malformed($"{what} is {value}, neither true nor false.");
            return value == 1;
        }

        public ulong ReadVarUInt()
        {
            ulong value = 0;
            for (int i = 0; i < MaxVarIntBytes; i++)
            {
                byte next = ReadByte();
                value |= (ulong)(next & 0x7F) << (7 * i);
                if ((next & 0x80) == 0) return value;
            }

            throw Malformed("A number runs on for more than ten bytes.");
        }

        public long ReadVarInt()
        {
            ulong zigzag = ReadVarUInt();
            return (long)(zigzag >> 1) ^ -(long)(zigzag & 1);
        }

        public int ReadInt(string what)
        {
            long value = ReadVarInt();
            if (value < int.MinValue || value > int.MaxValue) throw Malformed($"{what} ({value}) does not fit a 32-bit number.");
            return (int)value;
        }

        public uint ReadUInt(string what)
        {
            ulong value = ReadVarUInt();
            if (value > uint.MaxValue) throw Malformed($"{what} ({value}) does not fit an unsigned 32-bit number.");
            return (uint)value;
        }

        /// <summary>
        /// A list's length: at most <paramref name="max"/>, and never more entries than bytes left (every entry takes at least
        /// <paramref name="minBytesEach"/>), so a damaged length cannot make the reader allocate without bound.
        /// </summary>
        public int ReadCount(int max, string what, int minBytesEach = 1)
        {
            ulong count = ReadVarUInt();
            if (count > (ulong)max)
                throw new ProtocolException(ProtocolError.TooLarge, $"{what}: {count} entries is more than the protocol allows ({max}).");
            if (count * (ulong)Math.Max(minBytesEach, 0) > (ulong)RemainingBytes) throw Truncated();
            return (int)count;
        }

        public float ReadFloat() => BitConverter.Int32BitsToSingle((int)ReadFixed(4));

        public double ReadDouble() => BitConverter.Int64BitsToDouble((long)ReadFixed(8));

        public string ReadText(int maxBytes, string what)
        {
            ulong length = ReadVarUInt();
            if (length > (ulong)maxBytes)
                throw new ProtocolException(ProtocolError.TooLarge, $"{what}: {length} bytes of text is more than the protocol allows ({maxBytes}).");
            if (length > (ulong)RemainingBytes) throw Truncated();
            try
            {
                string text = new UTF8Encoding(false, true).GetString(_data, _position, (int)length);
                _position += (int)length;
                return text;
            }
            catch (DecoderFallbackException error)
            {
                throw new ProtocolException(ProtocolError.Malformed, $"{what} is not valid UTF-8 text.", error);
            }
        }

        /// <summary>An enum value written as its number; a number the enum does not name is malformed.</summary>
        public T ReadEnum<T>(string what) where T : struct, Enum
        {
            int number = ReadInt(what);
            object value = Enum.ToObject(typeof(T), number);
            if (!Enum.IsDefined(typeof(T), value)) throw Malformed($"{what} is {number}, which names no {typeof(T).Name}.");
            return (T)value;
        }

        public static ProtocolException Malformed(string message) => new ProtocolException(ProtocolError.Malformed, message);

        private ulong ReadFixed(int byteCount)
        {
            if (RemainingBytes < byteCount) throw Truncated();
            ulong bits = 0;
            for (int i = 0; i < byteCount; i++)
            {
                bits |= (ulong)_data[_position++] << (8 * i);
            }

            return bits;
        }

        private static ProtocolException Truncated() =>
            new ProtocolException(ProtocolError.Truncated, "The message ends before its contents do.");
    }
}
