using System;
using XRim.Core;

namespace XRim.Rules.Limbs
{
    /// <summary>One severed arm or leg: whose it was and which. It lies on the floor or is wielded as a club (GDD §12).</summary>
    public readonly struct SeveredLimbRef : IEquatable<SeveredLimbRef>
    {
        /// <summary>The dummy the limb was cut from.</summary>
        public Side Owner { get; }

        public BodyPart Part { get; }

        public SeveredLimbRef(Side owner, BodyPart part)
        {
            if (!part.IsSeverable()) throw new ArgumentException($"{part} cannot be severed (GDD §11).", nameof(part));
            Owner = owner;
            Part = part;
        }

        public bool Equals(SeveredLimbRef other) => Owner == other.Owner && Part == other.Part;
        public override bool Equals(object obj) => obj is SeveredLimbRef other && Equals(other);
        public override int GetHashCode() => (int)Owner * BodyParts.Count + (int)Part;
        public static bool operator ==(SeveredLimbRef a, SeveredLimbRef b) => a.Equals(b);
        public static bool operator !=(SeveredLimbRef a, SeveredLimbRef b) => !a.Equals(b);
        public override string ToString() => $"{Owner} {Part}";
    }
}
