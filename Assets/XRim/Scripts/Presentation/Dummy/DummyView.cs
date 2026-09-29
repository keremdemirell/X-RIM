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
        [Tooltip("One transform per body part, in order: Head, Torso, LeftArm, RightArm, LeftLeg, RightLeg.")]
        [SerializeField] private Transform[] _parts = new Transform[BodyParts.Count];

        [SerializeField] private Transform _heldItem;

        public void ApplyPose(FighterPose pose, ArenaSpace space)
        {
            int count = Mathf.Min(_parts.Length, pose.Parts.Length);
            for (int i = 0; i < count; i++)
            {
                Apply(_parts[i], pose.Parts[i], space);
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
