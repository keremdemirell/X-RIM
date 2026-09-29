using System;
using System.Globalization;

namespace XRim.Core
{
    /// <summary>
    /// A point on the simulation clock in integer microseconds. Time-to-impact, priority and the
    /// sudden-death tie-break compare these values (GDD §9, §14), so they must never drift.
    /// </summary>
    public readonly struct SimTime : IEquatable<SimTime>, IComparable<SimTime>
    {
        public const long MicrosecondsPerSecond = 1_000_000L;
        public const long MicrosecondsPerMillisecond = 1_000L;

        public static SimTime Zero => new SimTime(0L);

        public long Microseconds { get; }

        public SimTime(long microseconds)
        {
            Microseconds = microseconds;
        }

        public double Seconds => (double)Microseconds / MicrosecondsPerSecond;
        public double Milliseconds => (double)Microseconds / MicrosecondsPerMillisecond;

        public static SimTime FromSeconds(double seconds) =>
            new SimTime((long)Math.Round(seconds * MicrosecondsPerSecond, MidpointRounding.AwayFromZero));

        public static SimTime FromMilliseconds(double milliseconds) =>
            new SimTime((long)Math.Round(milliseconds * MicrosecondsPerMillisecond, MidpointRounding.AwayFromZero));

        public static SimTime operator +(SimTime a, SimTime b) => new SimTime(a.Microseconds + b.Microseconds);
        public static SimTime operator -(SimTime a, SimTime b) => new SimTime(a.Microseconds - b.Microseconds);
        public static bool operator <(SimTime a, SimTime b) => a.Microseconds < b.Microseconds;
        public static bool operator >(SimTime a, SimTime b) => a.Microseconds > b.Microseconds;
        public static bool operator <=(SimTime a, SimTime b) => a.Microseconds <= b.Microseconds;
        public static bool operator >=(SimTime a, SimTime b) => a.Microseconds >= b.Microseconds;
        public static bool operator ==(SimTime a, SimTime b) => a.Microseconds == b.Microseconds;
        public static bool operator !=(SimTime a, SimTime b) => a.Microseconds != b.Microseconds;

        public int CompareTo(SimTime other) => Microseconds.CompareTo(other.Microseconds);
        public bool Equals(SimTime other) => Microseconds == other.Microseconds;
        public override bool Equals(object obj) => obj is SimTime other && Equals(other);
        public override int GetHashCode() => Microseconds.GetHashCode();

        public override string ToString() => string.Format(CultureInfo.InvariantCulture, "{0:0.000} ms", Milliseconds);
    }
}
