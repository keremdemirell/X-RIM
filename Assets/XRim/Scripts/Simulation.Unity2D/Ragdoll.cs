using UnityEngine;
using XRim.Rules;
using XRim.Simulation.Physics;

namespace XRim.Simulation.Unity2D
{
    /// <summary>
    /// A dummy's physics body: one Rigidbody2D per <see cref="BodyPart"/>, limbs joined to the torso by HingeJoint2D.
    /// Severing is driven by game logic (limb durability reached 0), never by HingeJoint2D.breakForce, which the GDD
    /// rejects because physics-driven breaks are hard to balance (§18).
    /// </summary>
    public sealed class Ragdoll : MonoBehaviour
    {
        private readonly PhysicsBodyTag[] _parts = new PhysicsBodyTag[BodyParts.Count];

        private void Awake()
        {
            foreach (PhysicsBodyTag body in GetComponentsInChildren<PhysicsBodyTag>(true))
            {
                if (body.Role == BodyRole.BodyPart) _parts[(int)body.Part] = body;
            }
        }

        public Rigidbody2D GetBody(BodyPart part)
        {
            PhysicsBodyTag body = _parts[(int)part];
            return body != null ? body.GetComponent<Rigidbody2D>() : null;
        }

        /// <summary>Disables the joint that attaches a severed limb, so it falls and stays on the floor (GDD §12).</summary>
        public void BreakJoint(BodyPart part)
        {
            PhysicsBodyTag body = _parts[(int)part];
            if (body != null && body.TryGetComponent(out HingeJoint2D joint))
            {
                joint.enabled = false;
            }
        }
    }
}
