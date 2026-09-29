using System;
using System.Globalization;

namespace XRim.Economy
{
    /// <summary>
    /// An amount of earned currency (name TBD, GDD §16). Obtained only by playing; never sold for real money.
    /// A separate type from <see cref="PremiumAmount"/> so the two can never be mixed or converted by accident.
    /// </summary>
    public readonly struct EarnedAmount : IEquatable<EarnedAmount>, IComparable<EarnedAmount>
    {
        public long Value { get; }

        public EarnedAmount(long value)
        {
            if (value < 0) throw new ArgumentOutOfRangeException(nameof(value), value, "Currency amounts cannot be negative.");
            Value = value;
        }

        public static EarnedAmount Zero => new EarnedAmount(0);

        public static EarnedAmount operator +(EarnedAmount a, EarnedAmount b) => new EarnedAmount(a.Value + b.Value);
        public static bool operator >=(EarnedAmount a, EarnedAmount b) => a.Value >= b.Value;
        public static bool operator <=(EarnedAmount a, EarnedAmount b) => a.Value <= b.Value;
        public static bool operator >(EarnedAmount a, EarnedAmount b) => a.Value > b.Value;
        public static bool operator <(EarnedAmount a, EarnedAmount b) => a.Value < b.Value;

        public int CompareTo(EarnedAmount other) => Value.CompareTo(other.Value);
        public bool Equals(EarnedAmount other) => Value == other.Value;
        public override bool Equals(object obj) => obj is EarnedAmount other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
    }
}
