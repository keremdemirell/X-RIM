using System;

namespace XRim.Core
{
    /// <summary>Small deterministic PRNG (xorshift32). Same seed, same sequence on every platform.</summary>
    public sealed class XorShiftRandom : IRandom
    {
        private const uint FallbackSeed = 0x9E3779B9u;
        private const int FloatMantissaShift = 8;
        private const float TwentyFourBitToUnitFloat = 1f / 16777216f;

        private uint _state;

        public XorShiftRandom(uint seed)
        {
            _state = seed == 0u ? FallbackSeed : seed;
        }

        public int NextInt(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive)
            {
                throw new ArgumentOutOfRangeException(nameof(maxExclusive), "maxExclusive must be greater than minInclusive.");
            }

            uint range = (uint)(maxExclusive - minInclusive);
            return minInclusive + (int)(NextUInt() % range);
        }

        // Top 24 bits only, so the result fits a float mantissa exactly and never rounds up to 1.
        public float NextFloat() => (NextUInt() >> FloatMantissaShift) * TwentyFourBitToUnitFloat;

        private uint NextUInt()
        {
            _state ^= _state << 13;
            _state ^= _state >> 17;
            _state ^= _state << 5;
            return _state;
        }
    }
}
