using System;
using System.Globalization;

namespace XRim.Economy
{
    /// <summary>
    /// An amount of premium currency (name TBD, GDD §16). Bought with real money; buys cosmetics only and can
    /// never be converted into earned currency (Decided).
    /// </summary>
    public readonly struct PremiumAmount : IEquatable<PremiumAmount>, IComparable<PremiumAmount>
    {
        public long Value { get; }

        public PremiumAmount(long value)
        {
            if (value < 0) throw new ArgumentOutOfRangeException(nameof(value), value, "Currency amounts cannot be negative.");
            Value = value;
        }

        public static PremiumAmount Zero => new PremiumAmount(0);

        public static PremiumAmount operator +(PremiumAmount a, PremiumAmount b) => new PremiumAmount(a.Value + b.Value);
        public static bool operator >=(PremiumAmount a, PremiumAmount b) => a.Value >= b.Value;
        public static bool operator <=(PremiumAmount a, PremiumAmount b) => a.Value <= b.Value;
        public static bool operator >(PremiumAmount a, PremiumAmount b) => a.Value > b.Value;
        public static bool operator <(PremiumAmount a, PremiumAmount b) => a.Value < b.Value;

        public int CompareTo(PremiumAmount other) => Value.CompareTo(other.Value);
        public bool Equals(PremiumAmount other) => Value == other.Value;
        public override bool Equals(object obj) => obj is PremiumAmount other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
    }
}
