using System.Globalization;

namespace XRim.Simulation.Physics
{
    /// <summary>
    /// Target joint angles for one arm or leg, in degrees relative to the parent body, using the <c>RagdollSettings</c>
    /// convention: 0 is the rest pose (hanging straight down), positive swings the limb toward the opponent, and a knee bends
    /// backward (negative). The lower angle (elbow or knee) only exists with ten bodies (D2).
    /// </summary>
    public readonly struct LimbAngles
    {
        public static LimbAngles Rest => new LimbAngles(0f, 0f);

        /// <summary>Shoulder or hip.</summary>
        public float UpperDegrees { get; }

        /// <summary>Elbow or knee; ignored with six bodies.</summary>
        public float LowerDegrees { get; }

        public LimbAngles(float upperDegrees, float lowerDegrees)
        {
            UpperDegrees = upperDegrees;
            LowerDegrees = lowerDegrees;
        }

        public override string ToString() =>
            string.Format(CultureInfo.InvariantCulture, "({0:0.#}°, {1:0.#}°)", UpperDegrees, LowerDegrees);
    }
}
