using System;
using XRim.Core;

namespace XRim.Presentation.Cameras
{
    /// <summary>What the camera shows, in the canonical (unflipped) arena: the point at the centre of the screen and the height shown.</summary>
    public readonly struct ViewFraming : IEquatable<ViewFraming>
    {
        /// <summary>Two framings this close (arena units) are the same picture.</summary>
        private const float SameUnits = 0.5f;

        public Vec2 CentreUnits { get; }
        public float HeightUnits { get; }

        public ViewFraming(Vec2 centreUnits, float heightUnits)
        {
            CentreUnits = centreUnits;
            HeightUnits = heightUnits;
        }

        /// <summary>The framing a share of the way from this one to another (0 = this, 1 = the other).</summary>
        public ViewFraming Lerp(ViewFraming to, float t) =>
            new ViewFraming(Vec2.Lerp(CentreUnits, to.CentreUnits, t), HeightUnits + (to.HeightUnits - HeightUnits) * t);

        /// <summary>True when the two show the same picture to within half an arena unit.</summary>
        public bool IsCloseTo(ViewFraming other) =>
            Vec2.Distance(CentreUnits, other.CentreUnits) <= SameUnits && Math.Abs(HeightUnits - other.HeightUnits) <= SameUnits;

        public bool Equals(ViewFraming other) => CentreUnits == other.CentreUnits && HeightUnits.Equals(other.HeightUnits);
        public override bool Equals(object obj) => obj is ViewFraming other && Equals(other);
        public override int GetHashCode() => (CentreUnits.GetHashCode() * 397) ^ HeightUnits.GetHashCode();
        public override string ToString() => $"centre {CentreUnits}, height {HeightUnits:0.#}";
    }
}
