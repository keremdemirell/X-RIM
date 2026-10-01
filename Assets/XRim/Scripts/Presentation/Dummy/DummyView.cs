using UnityEngine;
using XRim.Config;
using XRim.Rules;
using XRim.Simulation.Physics;

namespace XRim.Presentation.Dummy
{
    /// <summary>
    /// The visible dummy. It only copies recorded poses onto its transforms and has no physics of its own.
    /// Cosmetic skins swap what is drawn here; hitboxes live on the physics ragdoll, so skins can never change
    /// hitboxes, silhouette or reach (GDD §2, §16).
    /// </summary>
    public sealed class DummyView : MonoBehaviour
    {
        [Tooltip("One transform per body part, in order: Head, Torso, LeftArm, RightArm, LeftLeg, RightLeg. For split limbs, the upper segment.")]
        [SerializeField] private Transform[] _parts = new Transform[BodyParts.Count];

        [Tooltip("Ten-body dummies only (D2): the lower segment of each arm and leg, in the same order. Empty otherwise.")]
        [SerializeField] private Transform[] _lowerParts = new Transform[BodyParts.Count];

        [SerializeField] private Transform _heldItem;

        /// <summary>Wires a dummy built in code (debug tools). Arrays are indexed by <see cref="BodyPart"/>.</summary>
        public void Configure(Transform[] parts, Transform[] lowerParts)
        {
            _parts = parts;
            _lowerParts = lowerParts;
        }

        /// <summary>The visual of the item the dummy holds now (it changes with the weapon); null for none.</summary>
        public void SetHeldItem(Transform heldItem)
        {
            if (_heldItem != null && _heldItem != heldItem) _heldItem.gameObject.SetActive(false);
            _heldItem = heldItem;
        }

        public void ApplyPose(FighterPose pose, ArenaSpace space)
        {
            int count = Mathf.Min(_parts.Length, pose.Parts.Length);
            for (int i = 0; i < count; i++)
            {
                Apply(_parts[i], pose.Parts[i], space);
            }

            if (pose.HasLowerSegments && _lowerParts != null)
            {
                int lowerCount = Mathf.Min(_lowerParts.Length, pose.LowerSegments.Length);
                for (int i = 0; i < lowerCount; i++)
                {
                    Apply(_lowerParts[i], pose.LowerSegments[i], space);
                }
            }

            if (_heldItem == null) return;
            _heldItem.gameObject.SetActive(pose.HasHeldItem);
            if (pose.HasHeldItem) Apply(_heldItem, pose.HeldItem, space);
        }

        private static void Apply(Transform target, BodyPose pose, ArenaSpace space)
        {
            if (target == null) return;
            target.SetPositionAndRotation(space.ToWorld(pose.PositionUnits), Quaternion.Euler(0f, 0f, pose.RotationDegrees));
        }
    }
}
