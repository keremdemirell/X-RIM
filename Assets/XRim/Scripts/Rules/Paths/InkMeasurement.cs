using System.Collections.Generic;

namespace XRim.Rules.Paths
{
    /// <summary>Result of measuring a path against a weapon's ink budget (GDD §6).</summary>
    public readonly struct InkMeasurement
    {
        public const int NoInvalidPoint = -1;

        private static readonly float[] NoPoints = new float[0];

        private readonly float[] _cumulativeCostUnits;

        /// <summary>Plain geometric length of the path.</summary>
        public float LengthUnits { get; }

        /// <summary>Ink spent, which can exceed the length when a bend penalty applies.</summary>
        public float CostUnits { get; }

        /// <summary>Index of the first point that broke the path (e.g. a too-sharp turn), or <see cref="NoInvalidPoint"/>.</summary>
        public int FirstInvalidPointIndex { get; }

        /// <summary>Ink spent from the start of the path up to each point; the first entry is 0.</summary>
        public IReadOnlyList<float> CumulativeCostUnits => _cumulativeCostUnits ?? NoPoints;

        public bool IsValid => FirstInvalidPointIndex == NoInvalidPoint;

        public InkMeasurement(float lengthUnits, float[] cumulativeCostUnits, int firstInvalidPointIndex)
        {
            _cumulativeCostUnits = cumulativeCostUnits ?? NoPoints;
            LengthUnits = lengthUnits;
            CostUnits = _cumulativeCostUnits.Length > 0 ? _cumulativeCostUnits[_cumulativeCostUnits.Length - 1] : 0f;
            FirstInvalidPointIndex = firstInvalidPointIndex;
        }
    }
}
