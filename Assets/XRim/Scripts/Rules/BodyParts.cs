using System.Collections.Generic;

namespace XRim.Rules
{
    public static class BodyParts
    {
        public const int Count = 6;

        public static IReadOnlyList<BodyPart> All { get; } = new[]
        {
            BodyPart.Head, BodyPart.Torso, BodyPart.LeftArm, BodyPart.RightArm, BodyPart.LeftLeg, BodyPart.RightLeg,
        };

        public static HitZone ToHitZone(this BodyPart part)
        {
            switch (part)
            {
                case BodyPart.Head: return HitZone.Head;
                case BodyPart.Torso: return HitZone.Torso;
                case BodyPart.LeftArm:
                case BodyPart.RightArm: return HitZone.Arm;
                default: return HitZone.Leg;
            }
        }

        /// <summary>GDD §11 (Decided): arms and legs can be severed; head and torso cannot.</summary>
        public static bool IsSeverable(this BodyPart part) => part.IsArm() || part.IsLeg();

        public static bool IsArm(this BodyPart part) => part == BodyPart.LeftArm || part == BodyPart.RightArm;

        public static bool IsLeg(this BodyPart part) => part == BodyPart.LeftLeg || part == BodyPart.RightLeg;

        /// <summary>GDD §12 (Decided): the weapon is held in the dominant hand while that arm is intact.</summary>
        public static BodyPart DominantArm(Handedness handedness) =>
            handedness == Handedness.Right ? BodyPart.RightArm : BodyPart.LeftArm;

        public static BodyPart OffArm(Handedness handedness) =>
            handedness == Handedness.Right ? BodyPart.LeftArm : BodyPart.RightArm;
    }
}
