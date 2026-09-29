using UnityEngine;

namespace XRim.Simulation.Unity2D
{
    /// <summary>
    /// The primitive sprites a placeholder dummy is drawn with. Each sprite is one world unit across. Visual only:
    /// the physics never reads them, and tests build dummies without any.
    /// </summary>
    public sealed class RagdollSprites
    {
        public Sprite Square { get; }
        public Sprite Circle { get; }

        /// <summary>The yellow-and-black calibration marker that shows a hit zone (GDD §2, Decided).</summary>
        public Sprite Marker { get; }

        public RagdollSprites(Sprite square, Sprite circle, Sprite marker)
        {
            Square = square;
            Circle = circle;
            Marker = marker;
        }
    }
}
