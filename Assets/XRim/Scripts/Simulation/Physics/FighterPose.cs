using System;
using XRim.Rules;

namespace XRim.Simulation.Physics
{
    /// <summary>The frozen pose of one dummy: every body part plus the held item (GDD §3 stance persistence).</summary>
    [Serializable]
    public sealed class FighterPose
    {
        /// <summary>Indexed by <see cref="BodyPart"/>. For a split limb (ten bodies) this is the upper segment.</summary>
        public BodyPose[] Parts = new BodyPose[BodyParts.Count];

        /// <summary>
        /// Ten-body ragdolls only (D2): the lower segment of each arm and leg (forearm, shin), indexed by
        /// <see cref="BodyPart"/>. Both segments belong to the same hit zone. Unused for the head and torso.
        /// </summary>
        public BodyPose[] LowerSegments = new BodyPose[BodyParts.Count];

        public bool HasLowerSegments;

        public BodyPose HeldItem;
        public bool HasHeldItem = true;

        public BodyPose Get(BodyPart part) => Parts[(int)part];

        public void Set(BodyPart part, BodyPose pose) => Parts[(int)part] = pose;

        public BodyPose GetLower(BodyPart part) => LowerSegments[(int)part];

        public void SetLower(BodyPart part, BodyPose pose) => LowerSegments[(int)part] = pose;

        public FighterPose Clone()
        {
            var copy = new FighterPose { HeldItem = HeldItem, HasHeldItem = HasHeldItem, HasLowerSegments = HasLowerSegments };
            Parts.CopyTo(copy.Parts, 0);
            LowerSegments.CopyTo(copy.LowerSegments, 0);
            return copy;
        }
    }
}
