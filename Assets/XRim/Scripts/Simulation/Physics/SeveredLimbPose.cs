using System;
using XRim.Core;
using XRim.Rules;
using XRim.Rules.Limbs;

namespace XRim.Simulation.Physics
{
    /// <summary>
    /// A severed limb lying on the arena floor; it stays there and can become a club (GDD §2, §12). Its pose means the same as the
    /// dummy's own limb: pivot at the end that was jointed, the limb along its local −Y. With ten bodies it keeps its lower segment,
    /// so it lies bent the way it fell (S1).
    /// </summary>
    [Serializable]
    public struct SeveredLimbPose
    {
        public Side Owner;
        public BodyPart Part;

        /// <summary>The limb, or its upper segment with ten bodies.</summary>
        public BodyPose Pose;

        /// <summary>The forearm or shin (ten bodies only).</summary>
        public BodyPose Lower;
        public bool HasLower;

        public SeveredLimbPose(Side owner, BodyPart part, BodyPose pose)
        {
            Owner = owner;
            Part = part;
            Pose = pose;
            Lower = default;
            HasLower = false;
        }

        public SeveredLimbPose(Side owner, BodyPart part, BodyPose pose, BodyPose lower) : this(owner, part, pose)
        {
            Lower = lower;
            HasLower = true;
        }

        public SeveredLimbRef Limb => new SeveredLimbRef(Owner, Part);
    }
}
