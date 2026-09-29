using UnityEngine;
using XRim.Core;
using XRim.Rules;
using XRim.Simulation.Physics;

namespace XRim.Simulation.Unity2D
{
    /// <summary>
    /// Marks a Rigidbody2D so contacts can be reported to the rules as "whose body part / weapon / wall".
    /// Hitboxes live only on the physics ragdoll; cosmetic skins never add or change these (GDD §2, §16).
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class PhysicsBodyTag : MonoBehaviour
    {
        [SerializeField] private bool _hasOwner = true;
        [SerializeField] private Side _owner;
        [SerializeField] private BodyRole _role = BodyRole.BodyPart;

        [Tooltip("Used when the role is BodyPart or SeveredLimb.")]
        [SerializeField] private BodyPart _part;

        public BodyTag Tag => new BodyTag(_hasOwner ? _owner : (Side?)null, _role, _part);
        public BodyPart Part => _part;
        public BodyRole Role => _role;

        public void Configure(Side? owner, BodyRole role, BodyPart part)
        {
            _hasOwner = owner.HasValue;
            _owner = owner.GetValueOrDefault();
            _role = role;
            _part = part;
        }
    }
}
