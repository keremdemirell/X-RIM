using System;
using XRim.Rules;

namespace XRim.Simulation.Physics
{
    /// <summary>The frozen pose of one dummy: every body part plus the held item (GDD §3 stance persistence).</summary>
    [Serializable]
    public sealed class FighterPose
    {
        /// <summary>Indexed by <see cref="BodyPart"/>.</summary>
        public BodyPose[] Parts = new BodyPose[BodyParts.Count];

        public BodyPose HeldItem;
        public bool HasHeldItem = true;

        public BodyPose Get(BodyPart part) => Parts[(int)part];

        public void Set(BodyPart part, BodyPose pose) => Parts[(int)part] = pose;

        public FighterPose Clone()
        {
            var copy = new FighterPose { HeldItem = HeldItem, HasHeldItem = HasHeldItem };
            Parts.CopyTo(copy.Parts, 0);
            return copy;
        }
    }
}
