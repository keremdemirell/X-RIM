using System;

namespace XRim.Rules
{
    /// <summary>Stable id of a weapon definition, e.g. "rapier". See <see cref="WeaponIds"/>.</summary>
    public readonly struct WeaponId : IEquatable<WeaponId>
    {
        public string Value { get; }

        public WeaponId(string value)
        {
            Value = value ?? string.Empty;
        }

        public bool IsEmpty => string.IsNullOrEmpty(Value);

        public bool Equals(WeaponId other) => string.Equals(Value ?? string.Empty, other.Value ?? string.Empty, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is WeaponId other && Equals(other);
        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value ?? string.Empty);
        public static bool operator ==(WeaponId a, WeaponId b) => a.Equals(b);
        public static bool operator !=(WeaponId a, WeaponId b) => !a.Equals(b);
        public override string ToString() => Value ?? string.Empty;
    }
}
