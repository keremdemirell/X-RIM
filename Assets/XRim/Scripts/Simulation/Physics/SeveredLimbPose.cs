using System;
using XRim.Core;
using XRim.Rules;

namespace XRim.Simulation.Physics
{
    /// <summary>A severed limb lying on the arena floor; it stays there and can become a club (GDD §2, §12).</summary>
    [Serializable]
    public struct SeveredLimbPose
    {
        public Side Owner;
        public BodyPart Part;
        public BodyPose Pose;

        public SeveredLimbPose(Side owner, BodyPart part, BodyPose pose)
        {
            Owner = owner;
            Part = part;
            Pose = pose;
        }
    }
}
