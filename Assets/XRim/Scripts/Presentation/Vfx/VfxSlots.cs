using System;

namespace XRim.Presentation.Vfx
{
    /// <summary>
    /// The pooled slots effect pieces are drawn in (presentation only): at most <see cref="Capacity"/> at once; when every slot is in use
    /// the oldest piece is reused, so a long fight never piles up debris or slows the phone down.
    /// </summary>
    public sealed class VfxSlots
    {
        private readonly long[] _spawnOrder;
        private readonly bool[] _used;
        private long _nextOrder;

        public VfxSlots(int capacity)
        {
            if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "At least one slot.");
            _spawnOrder = new long[capacity];
            _used = new bool[capacity];
        }

        public int Capacity => _used.Length;

        public int ActiveCount { get; private set; }

        public bool IsUsed(int slot) => _used[slot];

        /// <summary>A free slot, or the oldest one in use when none is free (its piece is replaced).</summary>
        public int Acquire()
        {
            int slot = -1;
            for (int i = 0; i < _used.Length; i++)
            {
                if (_used[i]) continue;
                slot = i;
                break;
            }

            if (slot < 0)
            {
                slot = 0;
                for (int i = 1; i < _used.Length; i++)
                {
                    if (_spawnOrder[i] < _spawnOrder[slot]) slot = i;
                }
            }
            else
            {
                ActiveCount++;
            }

            _used[slot] = true;
            _spawnOrder[slot] = _nextOrder++;
            return slot;
        }

        public void Release(int slot)
        {
            if (!_used[slot]) return;
            _used[slot] = false;
            ActiveCount--;
        }

        public void Clear()
        {
            Array.Clear(_used, 0, _used.Length);
            ActiveCount = 0;
        }
    }
}
