namespace XRim.Rules.Paths
{
    /// <summary>Result of measuring a path against a weapon's ink budget (GDD §6).</summary>
    public readonly struct InkMeasurement
    {
        public const int NoInvalidPoint = -1;

        /// <summary>Plain geometric length of the path.</summary>
        public float LengthUnits { get; }

        /// <summary>Ink spent, which can exceed the length when a bend penalty applies.</summary>
        public float CostUnits { get; }

        /// <summary>Index of the first point that broke the path (e.g. a too-sharp turn), or <see cref="NoInvalidPoint"/>.</summary>
        public int FirstInvalidPointIndex { get; }

        public bool IsValid => FirstInvalidPointIndex == NoInvalidPoint;

        public InkMeasurement(float lengthUnits, float costUnits, int firstInvalidPointIndex)
        {
            LengthUnits = lengthUnits;
            CostUnits = costUnits;
            FirstInvalidPointIndex = firstInvalidPointIndex;
        }
    }
}
