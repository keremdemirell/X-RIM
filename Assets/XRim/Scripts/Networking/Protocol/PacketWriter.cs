using System;
using System.Text;

namespace XRim.Networking.Protocol
{
    /// <summary>
    /// Builds a message body in bytes. Integers are variable-length (small values take one byte; signed ones zigzag so small negatives do
    /// too), floats and doubles are their exact bits in little-endian order, text is UTF-8 after its length. Writing something the protocol
    /// refuses (a list beyond its limit, text too long) is a bug in the sender and throws <see cref="ArgumentException"/>.
    /// </summary>
    internal sealed class PacketWriter
    {
        private const int InitialCapacityBytes = 256;

        private byte[] _buffer = new byte[InitialCapacityBytes];

        public int Length { get; private set; }

        /// <summary>The bytes written so far are <c>Buffer[0..Length)</c>.</summary>
        public byte[] Buffer => _buffer;

        public byte[] ToArray()
        {
            var copy = new byte[Length];
            Array.Copy(_buffer, copy, Length);
            return copy;
        }

        public void WriteByte(byte value)
        {
            Reserve(1);
            _buffer[Length++] = value;
        }

        public void WriteBool(bool value) => WriteByte(value ? (byte)1 : (byte)0);

        public void WriteVarUInt(ulong value)
        {
            while (value >= 0x80)
            {
                WriteByte((byte)(value | 0x80));
                value >>= 7;
            }

            WriteByte((byte)value);
        }

        /// <summary>A signed integer, zigzagged so values near zero take one byte whichever their sign.</summary>
        public void WriteVarInt(long value) => WriteVarUInt((ulong)((value << 1) ^ (value >> 63)));

        public void WriteInt(int value) => WriteVarInt(value);

        /// <summary>A list's length, which must not pass the protocol's limit for it.</summary>
        public void WriteCount(int count, int max, string what)
        {
            if (count < 0 || count > max)
                throw new ArgumentException($"{what}: {count} entries is more than the protocol allows ({max}).", nameof(count));
            WriteVarUInt((ulong)count);
        }

        public void WriteFloat(float value) => WriteFixed((uint)BitConverter.SingleToInt32Bits(value), 4);

        public void WriteDouble(double value) => WriteFixed((ulong)BitConverter.DoubleToInt64Bits(value), 8);

        public void WriteText(string value, int maxBytes, string what)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
            if (bytes.Length > maxBytes)
                throw new ArgumentException($"{what}: {bytes.Length} bytes of text is more than the protocol allows ({maxBytes}).", nameof(value));
            WriteVarUInt((ulong)bytes.Length);
            WriteBytes(bytes, 0, bytes.Length);
        }

        public void WriteBytes(byte[] bytes, int offset, int count)
        {
            Reserve(count);
            Array.Copy(bytes, offset, _buffer, Length, count);
            Length += count;
        }

        private void WriteFixed(ulong bits, int byteCount)
        {
            Reserve(byteCount);
            for (int i = 0; i < byteCount; i++)
            {
                _buffer[Length++] = (byte)(bits >> (8 * i));
            }
        }

        private void Reserve(int extra)
        {
            int needed = Length + extra;
            if (needed <= _buffer.Length) return;
            int capacity = _buffer.Length;
            while (capacity < needed) capacity *= 2;
            Array.Resize(ref _buffer, capacity);
        }
    }
}
