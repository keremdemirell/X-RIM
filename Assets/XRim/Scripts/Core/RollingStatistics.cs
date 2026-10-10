using System;
using System.Globalization;

namespace XRim.Core
{
    /// <summary>
    /// Measurements of one thing over time (a turn's simulation time, a frame's length): everything seen so far (count, smallest,
    /// mean, largest) and the most recent <c>capacity</c> values (their mean, largest and any percentile). Engine-free, so the arithmetic
    /// is testable; the measuring itself (a stopwatch) lives in the App and DebugTools layers, never in Rules (GDD §18). A value that
    /// is not a finite number is ignored.
    /// </summary>
    public sealed class RollingStatistics
    {
        private readonly double[] _window;
        private int _next;
        private double _sum;

        public RollingStatistics(int capacity)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity), "A window needs room for at least one value.");
            _window = new double[capacity];
        }

        /// <summary>How many values have been added in all.</summary>
        public int Count { get; private set; }

        /// <summary>How many of the most recent values are held (at most the capacity).</summary>
        public int WindowCount => Math.Min(Count, _window.Length);

        /// <summary>The newest value; 0 before the first.</summary>
        public double Last { get; private set; }

        public double AllTimeMin { get; private set; }
        public double AllTimeMax { get; private set; }
        public double AllTimeMean => Count > 0 ? _sum / Count : 0.0;

        /// <summary>The mean of the held values; 0 before the first.</summary>
        public double WindowMean
        {
            get
            {
                int held = WindowCount;
                double sum = 0.0;
                for (int i = 0; i < held; i++)
                {
                    sum += _window[i];
                }

                return held > 0 ? sum / held : 0.0;
            }
        }

        /// <summary>The largest of the held values; 0 before the first.</summary>
        public double WindowMax
        {
            get
            {
                int held = WindowCount;
                double max = 0.0;
                for (int i = 0; i < held; i++)
                {
                    if (i == 0 || _window[i] > max) max = _window[i];
                }

                return max;
            }
        }

        public void Add(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) return;

            _window[_next] = value;
            _next = (_next + 1) % _window.Length;
            Last = value;
            _sum += value;
            AllTimeMin = Count == 0 ? value : Math.Min(AllTimeMin, value);
            AllTimeMax = Count == 0 ? value : Math.Max(AllTimeMax, value);
            Count++;
        }

        public void Clear()
        {
            Count = 0;
            _next = 0;
            _sum = 0.0;
            Last = 0.0;
            AllTimeMin = 0.0;
            AllTimeMax = 0.0;
        }

        /// <summary>
        /// The value at or below which a share of the held values fall (nearest rank): 0.5 is the median, 0.95 the value 95% of them
        /// stay under. 0 before the first value.
        /// </summary>
        public double Percentile(double fraction)
        {
            int held = WindowCount;
            if (held == 0) return 0.0;

            var sorted = new double[held];
            Array.Copy(_window, sorted, held);
            Array.Sort(sorted);
            double clamped = Math.Min(Math.Max(fraction, 0.0), 1.0);
            int rank = (int)Math.Ceiling(clamped * held);
            return sorted[Math.Max(rank - 1, 0)];
        }

        /// <summary>One line for a log or a panel, in the invariant culture so it reads the same on every device.</summary>
        public string Describe(string unit)
        {
            if (Count == 0) return "no measurements yet";

            return string.Format(CultureInfo.InvariantCulture,
                "{0} measured · last {1:0.0} · mean {2:0.0} · 95% under {3:0.0} · min {4:0.0} · max {5:0.0} {6}",
                Count, Last, AllTimeMean, Percentile(0.95), AllTimeMin, AllTimeMax, unit);
        }
    }
}
