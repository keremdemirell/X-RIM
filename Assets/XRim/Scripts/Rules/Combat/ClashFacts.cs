namespace XRim.Rules.Combat
{
    /// <summary>
    /// Physics-free description of two weapons touching. The simulation layer measures it from 2D contact
    /// data (normal and relative velocity); the rules decide the outcome.
    /// </summary>
    public readonly struct ClashFacts
    {
        /// <summary>0° = sliding along each other, 90° = square impact (GDD §10 stage 1).</summary>
        public float ContactAngleDegrees { get; }

        public ClashParticipant Left { get; }
        public ClashParticipant Right { get; }

        public ClashFacts(float contactAngleDegrees, ClashParticipant left, ClashParticipant right)
        {
            ContactAngleDegrees = contactAngleDegrees;
            Left = left;
            Right = right;
        }
    }
}
