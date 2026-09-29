using System;

namespace XRim.Rules
{
    /// <summary>Stable id of a signature (joker) move definition (GDD §8).</summary>
    public readonly struct SignatureMoveId : IEquatable<SignatureMoveId>
    {
        public string Value { get; }

        public SignatureMoveId(string value)
        {
            Value = value ?? string.Empty;
        }

        public bool IsEmpty => string.IsNullOrEmpty(Value);

        public bool Equals(SignatureMoveId other) => string.Equals(Value ?? string.Empty, other.Value ?? string.Empty, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is SignatureMoveId other && Equals(other);
        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value ?? string.Empty);
        public static bool operator ==(SignatureMoveId a, SignatureMoveId b) => a.Equals(b);
        public static bool operator !=(SignatureMoveId a, SignatureMoveId b) => !a.Equals(b);
        public override string ToString() => Value ?? string.Empty;
    }
}
