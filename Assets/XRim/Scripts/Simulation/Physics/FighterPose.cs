using System;
using XRim.Rules;

namespace XRim.Simulation.Physics
{
    /// <summary>The frozen pose of one dummy: every body part plus the held item (GDD §3 stance persistence).</summary>
    [Serializable]
    public sealed class FighterPose
    {
        /// <summary>
        /// Indexed by <see cref="BodyPart"/>. For a split limb (ten bodies) this is the upper segment. A severed limb's entry is
        /// stale: the limb lies in <see cref="PoseSnapshot.SeveredLimbs"/>. With a split torso this is its bottom half.
        /// </summary>
        public BodyPose[] Parts = new BodyPose[BodyParts.Count];

        /// <summary>
        /// Ten-body ragdolls only (D2): the lower segment of each arm and leg (forearm, shin), indexed by
        /// <see cref="BodyPart"/>. Both segments belong to the same hit zone. Unused for the head and torso.
        /// </summary>
        public BodyPose[] LowerSegments = new BodyPose[BodyParts.Count];

        public bool HasLowerSegments;

        /// <summary>
        /// The held weapon, shield or club (grip at the origin, +X along it); for an armless body blow, the striking head in the same
        /// convention (<see cref="HeldItemShape.HeadAsItem"/>).
        /// </summary>
        public BodyPose HeldItem;
        public bool HasHeldItem = true;

        /// <summary>
        /// The item this dummy dropped this turn when the arm holding it was severed (GDD §12): it falls under physics and is drawn
        /// where it lies. It is never rebuilt by the next load.
        /// </summary>
        public BodyPose DroppedItem;
        public bool HasDroppedItem;

        /// <summary>The dummy died: its torso split in two (GDD §11). <see cref="Parts"/> holds the bottom half, this the top half.</summary>
        public bool IsTorsoSplit;
        public BodyPose TorsoTop;

        /// <summary>
        /// The limbs no longer on this dummy (GDD §12), one bit per <see cref="BodyPart"/>, as the rules had them when the pose was
        /// captured: each lies on the floor (<see cref="PoseSnapshot.SeveredLimbs"/>) or is wielded as a club. Views draw from it.
        /// </summary>
        public int SeveredParts;

        public bool IsSevered(BodyPart part) => (SeveredParts & (1 << (int)part)) != 0;

        public void MarkSevered(BodyPart part) => SeveredParts |= 1 << (int)part;

        public BodyPose Get(BodyPart part) => Parts[(int)part];

        public void Set(BodyPart part, BodyPose pose) => Parts[(int)part] = pose;

        public BodyPose GetLower(BodyPart part) => LowerSegments[(int)part];

        public void SetLower(BodyPart part, BodyPose pose) => LowerSegments[(int)part] = pose;

        public FighterPose Clone()
        {
            var copy = new FighterPose
            {
                HeldItem = HeldItem, HasHeldItem = HasHeldItem, HasLowerSegments = HasLowerSegments, DroppedItem = DroppedItem,
                HasDroppedItem = HasDroppedItem, IsTorsoSplit = IsTorsoSplit, TorsoTop = TorsoTop, SeveredParts = SeveredParts,
            };
            Parts.CopyTo(copy.Parts, 0);
            LowerSegments.CopyTo(copy.LowerSegments, 0);
            return copy;
        }
    }
}
