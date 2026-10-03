using System;
using System.Collections.Generic;
using UnityEngine;
using XRim.Config;
using XRim.Core;
using XRim.Rules;
using XRim.Rules.Settings;
using XRim.Simulation.Physics;
using XRim.Simulation.Settings;

namespace XRim.Simulation.Unity2D
{
    /// <summary>
    /// A dummy's physics body: Rigidbody2D parts joined by HingeJoint2D (GDD §18), one body per <see cref="BodyPart"/>
    /// or, with ten bodies (D2), upper and lower segments of each arm and leg that belong to the same hit zone.
    /// Every body is a direct child of this object, whose origin is the pelvis (the torso frame's origin).
    /// The prefab is built facing +X; <see cref="MirrorForRightSide"/> turns an instance to face -X.
    /// The weapon arm follows the held weapon: a spring (TargetJoint2D) pulls its hand onto the blade at the point the
    /// arm can reach (<see cref="ArmReach"/>). The weapon feels nothing back, so the arm can never block or yank it.
    /// Severing is driven by game logic (limb durability reached 0), never by HingeJoint2D.breakForce, which the GDD
    /// rejects because physics-driven breaks are hard to balance (§18).
    /// </summary>
    public sealed class Ragdoll : MonoBehaviour
    {
        /// <summary>How far the built reach may differ from the current path settings before it counts as stale.</summary>
        private const float ReachToleranceUnits = 0.5f;

        /// <summary>
        /// Whether Unity's HingeJoint2D.jointAngle grows as the jointed limb turns counter-clockwise relative to its
        /// parent. <see cref="RagdollSettings"/> limits are written that way (positive = forward for a dummy facing +X);
        /// Unity 6.5 measures the other way (2026-10-01, PlayMode test JointAngle_GrowsWhenALimbSwingsForward), so limits
        /// are flipped when applied.
        /// </summary>
        public static bool JointAngleGrowsCounterClockwise => false;

        [SerializeField] private RagdollSegmentation _segmentation = RagdollSegmentation.SixBodies;
        [SerializeField] private Rigidbody2D[] _upper = new Rigidbody2D[BodyParts.Count];
        [SerializeField] private Rigidbody2D[] _lower = new Rigidbody2D[BodyParts.Count];

        [Tooltip("The grip point at the end of each arm, on the arm's last segment. Empty for the head, torso and legs.")]
        [SerializeField] private Transform[] _hands = new Transform[BodyParts.Count];

        [SerializeField] private List<HeldItemSlot> _heldItems = new List<HeldItemSlot>();

        [Header("As built (checked against the live settings at load)")]
        [SerializeField] private float _worldUnitsPerArenaUnit;
        [SerializeField] private Vector2 _shoulderOffsetUnits;
        [SerializeField] private float _armLengthUnits;

        private readonly List<JointEntry> _joints = new List<JointEntry>();
        private readonly List<Collider2D> _colliderBuffer = new List<Collider2D>();

        /// <summary>Each joint's servo target, as Unity measures the joint angle; parallel to <see cref="_joints"/>.</summary>
        private float[] _jointTargets = Array.Empty<float>();

        private bool _jointsFound;
        private float _elbowMinDegrees;
        private float _elbowMaxDegrees;
        private Rigidbody2D _heldItem;
        private TargetJoint2D _handFollow;
        private BodyPart _heldArm;

        public RagdollSegmentation Segmentation => _segmentation;
        public bool HasLowerSegments => _segmentation == RagdollSegmentation.TenBodies;
        public bool IsMirrored { get; private set; }
        public IReadOnlyList<HeldItemSlot> HeldItems => _heldItems;

        /// <summary>The held weapon's body while it is held; null otherwise.</summary>
        public Rigidbody2D ActiveHeldItem => _handFollow != null ? _heldItem : null;

        /// <summary>Called once by <see cref="PlaceholderRagdollBuilder"/>.</summary>
        internal void Configure(RagdollSegmentation segmentation, Rigidbody2D[] upper, Rigidbody2D[] lower, Transform[] hands,
            List<HeldItemSlot> heldItems, ArenaSpace space, PathSettings paths)
        {
            _segmentation = segmentation;
            _upper = upper;
            _lower = lower;
            _hands = hands;
            _heldItems = heldItems;
            _worldUnitsPerArenaUnit = space.WorldUnitsPerArenaUnit;
            _shoulderOffsetUnits = new Vector2(paths.ShoulderOffsetUnits.X, paths.ShoulderOffsetUnits.Y);
            _armLengthUnits = paths.ArmLengthUnits;
            _jointsFound = false;
        }

        /// <summary>The body of a part; for a split limb, its upper segment.</summary>
        public Rigidbody2D GetBody(BodyPart part) => _upper[(int)part];

        /// <summary>The lower segment of a split limb; null with six bodies and for the head and torso.</summary>
        public Rigidbody2D GetLowerBody(BodyPart part) => HasLowerSegments ? _lower[(int)part] : null;

        /// <summary>The grip point of an arm, on its last segment.</summary>
        public Transform GetHand(BodyPart arm) => _hands[(int)arm];

        public Rigidbody2D FindHeldItem(string weaponId)
        {
            foreach (HeldItemSlot slot in _heldItems)
            {
                if (string.Equals(slot.WeaponId, weaponId, StringComparison.Ordinal)) return slot.Body;
            }

            return null;
        }

        /// <summary>Every body-part body (both segments of split limbs), in BodyPart order, upper before lower.</summary>
        public void GetPartBodies(List<Rigidbody2D> into)
        {
            foreach (BodyPart part in BodyParts.All)
            {
                if (_upper[(int)part] != null) into.Add(_upper[(int)part]);
                Rigidbody2D lower = GetLowerBody(part);
                if (lower != null) into.Add(lower);
            }
        }

        /// <summary>
        /// False when the prefab was built with a different shoulder, arm length or scale than the live settings, so its
        /// reach no longer matches the §6 reach limit. Rebuild it with XRim/Setup/Build Placeholder Dummies.
        /// </summary>
        public bool MatchesReach(PathSettings paths, ArenaSpace space)
        {
            return Math.Abs(_armLengthUnits - paths.ArmLengthUnits) <= ReachToleranceUnits &&
                   Math.Abs(_shoulderOffsetUnits.x - paths.ShoulderOffsetUnits.X) <= ReachToleranceUnits &&
                   Math.Abs(_shoulderOffsetUnits.y - paths.ShoulderOffsetUnits.Y) <= ReachToleranceUnits &&
                   Mathf.Approximately(_worldUnitsPerArenaUnit, space.WorldUnitsPerArenaUnit);
        }

        /// <summary>Tags every body with its owner, so contacts can be reported as "whose part or weapon".</summary>
        public void AssignOwner(Side side)
        {
            foreach (BodyPart part in BodyParts.All)
            {
                Tag(_upper[(int)part], side, BodyRole.BodyPart, part);
                Tag(GetLowerBody(part), side, BodyRole.BodyPart, part);
            }

            foreach (HeldItemSlot slot in _heldItems)
            {
                Tag(slot.Body, side, BodyRole.HeldItem, BodyPart.Torso);
            }
        }

        /// <summary>
        /// Turns this instance to face -X (the right-hand fighter). Call once, on an instance still in its built layout,
        /// before holding, posing or tuning. Every placeholder shape is symmetric about its body's vertical axis, so only
        /// positions, anchors and joint limits change.
        /// </summary>
        public void MirrorForRightSide()
        {
            if (IsMirrored) return;
            IsMirrored = true;

            var bodies = new List<Rigidbody2D>();
            GetPartBodies(bodies);
            foreach (HeldItemSlot slot in _heldItems)
            {
                if (slot.Body != null) bodies.Add(slot.Body);
            }

            foreach (Rigidbody2D body in bodies)
            {
                body.transform.localPosition = MirrorX(body.transform.localPosition);
            }

            foreach (Transform hand in _hands)
            {
                if (hand != null) hand.localPosition = MirrorX(hand.localPosition);
            }

            foreach (JointEntry entry in Joints())
            {
                entry.Joint.anchor = MirrorX(entry.Joint.anchor);
                entry.Joint.connectedAnchor = MirrorX(entry.Joint.connectedAnchor);
            }
        }

        /// <summary>
        /// Takes a weapon in an arm's hand (GDD §12: the dominant hand): activates its body at the hand of the built layout
        /// and makes the hand follow it. Call on an instance still in its built layout, before <see cref="ApplyTuning"/>.
        /// Returns null when this prefab has no body for the weapon (rebuild the dummies).
        /// </summary>
        public Rigidbody2D Hold(string weaponId, BodyPart arm)
        {
            Rigidbody2D item = FindHeldItem(weaponId);
            Transform hand = _hands[(int)arm];
            if (item == null || hand == null) return null;

            Rigidbody2D lastSegment = LastSegment(arm);
            item.transform.localPosition = lastSegment.transform.localPosition + hand.localPosition;
            item.transform.localRotation = Quaternion.Euler(0f, 0f, IsMirrored ? 180f : 0f);
            item.gameObject.SetActive(true);

            _handFollow = lastSegment.gameObject.AddComponent<TargetJoint2D>();
            _handFollow.autoConfigureTarget = false;
            _handFollow.anchor = hand.localPosition;
            _handFollow.target = hand.position;
            _heldItem = item;
            _heldArm = arm;
            return item;
        }

        /// <summary>Lets go of the held weapon: it falls under physics and the arm stops following it (GDD §12).</summary>
        public void DropHeldItem()
        {
            // A whole turn is simulated within one frame, so the joint is switched off now and destroyed later.
            if (_handFollow != null)
            {
                _handFollow.enabled = false;
                Destroy(_handFollow);
            }

            _handFollow = null;
            if (_heldItem != null) _heldItem.bodyType = RigidbodyType2D.Dynamic;
        }

        /// <summary>
        /// Points the hand spring at the place on the blade the arm can reach (see <see cref="ArmReach"/>). Call after
        /// posing and before every physics step.
        /// </summary>
        public void UpdateGrip()
        {
            if (_handFollow == null || _heldItem == null) return;
            Rigidbody2D shoulder = _upper[(int)_heldArm];
            float blade = _heldItem.TryGetComponent(out BoxCollider2D box) ? box.size.x : 0f;
            Vector2 axis = DirectionOf(_heldItem.rotation);
            HandReach(_heldArm, out float minReach, out float maxReach);
            float along = ArmReach.HandAlongBlade(ToVec2(shoulder.position), ToVec2(_heldItem.position), ToVec2(axis), blade,
                minReach, maxReach);
            _handFollow.target = _heldItem.position + axis * along;
        }

        /// <summary>
        /// Places every body at a pose (arena units) with zero velocity. A pose without lower segments places them in
        /// line with their upper segments. Call on an instance still in its built layout.
        /// </summary>
        public void ApplyPose(FighterPose pose, ArenaSpace space)
        {
            Guard.NotNull(pose, nameof(pose));
            foreach (BodyPart part in BodyParts.All)
            {
                Rigidbody2D upper = _upper[(int)part];
                Rigidbody2D lower = GetLowerBody(part);
                BodyPose upperPose = pose.Get(part);
                if (lower != null)
                {
                    BodyPose lowerPose = pose.HasLowerSegments ? pose.GetLower(part) : InLineWith(upperPose, upper, lower, space);
                    SetBody(lower, lowerPose, space);
                }

                SetBody(upper, upperPose, space);
            }

            if (ActiveHeldItem != null && pose.HasHeldItem) SetBody(_heldItem, pose.HeldItem, space);
            UpdateGrip();
        }

        /// <summary>One dummy never collides with itself, including its own weapon (only other bodies are hit).</summary>
        public void IgnoreSelfCollisions()
        {
            var colliders = new List<Collider2D>();
            var bodies = new List<Rigidbody2D>();
            GetPartBodies(bodies);
            if (ActiveHeldItem != null) bodies.Add(_heldItem);
            foreach (Rigidbody2D body in bodies)
            {
                _colliderBuffer.Clear();
                body.GetAttachedColliders(_colliderBuffer);
                colliders.AddRange(_colliderBuffer);
            }

            for (int i = 0; i < colliders.Count; i++)
            {
                for (int j = i + 1; j < colliders.Count; j++)
                {
                    Physics2D.IgnoreCollision(colliders[i], colliders[j], true);
                }
            }
        }

        /// <summary>
        /// Applies the live masses, joint limits and pose-holding strength (they can change between turns without a
        /// rebuild), and gravity. <paramref name="limpArm"/> is the arm the held weapon carries: its motors are off unless
        /// <see cref="RagdollSettings.ServoWeaponArm"/> is set, so they never fight the weapon's path.
        /// </summary>
        public void ApplyTuning(RagdollSettings body, float gravityScale, BodyPart? limpArm)
        {
            Guard.NotNull(body, nameof(body));
            _elbowMinDegrees = body.ElbowMinDegrees;
            _elbowMaxDegrees = body.ElbowMaxDegrees;
            float upperShare = HasLowerSegments ? body.UpperSegmentFraction : 1f;
            SetMass(BodyPart.Head, body.HeadMass, 1f);
            SetMass(BodyPart.Torso, body.TorsoMass, 1f);
            foreach (BodyPart part in BodyParts.All)
            {
                if (part.IsArm()) SetMass(part, body.ArmMass, upperShare);
                else if (part.IsLeg()) SetMass(part, body.LegMass, upperShare);
            }

            float maxAngularAcceleration = body.JointServoMaxAngularAccelerationDegreesPerSecondSquared * Mathf.Deg2Rad;
            foreach (JointEntry entry in Joints())
            {
                ApplyLimits(entry, body);
                JointMotor2D motor = entry.Joint.motor;
                motor.motorSpeed = 0f;
                motor.maxMotorTorque = entry.Body.inertia * maxAngularAcceleration;
                entry.Joint.motor = motor;
                bool carriedByWeapon = limpArm.HasValue && entry.Part == limpArm.Value && !body.ServoWeaponArm;
                entry.Joint.useMotor = body.JointServoGainPerSecond > 0f && !carriedByWeapon;
            }

            var bodies = new List<Rigidbody2D>();
            GetPartBodies(bodies);
            foreach (HeldItemSlot slot in _heldItems)
            {
                if (slot.Body != null) bodies.Add(slot.Body);
            }

            foreach (Rigidbody2D rigidbody in bodies)
            {
                rigidbody.gravityScale = gravityScale;
            }

            if (_handFollow != null)
            {
                Rigidbody2D hand = LastSegment(_heldArm);
                _handFollow.frequency = body.WeaponArmFollowFrequencyHz;
                _handFollow.dampingRatio = body.WeaponArmFollowDampingRatio;
                _handFollow.maxForce = hand.mass * body.WeaponArmFollowMaxAccelerationUnitsPerSecondSquared * _worldUnitsPerArenaUnit;
            }
        }

        /// <summary>
        /// The angles a limb's joint motors turn toward (a body move bends the legs, GDD §5), in the <see cref="LimbAngles"/>
        /// convention: the shoulder or hip takes the upper angle, the elbow or knee the lower one. A new instance holds every
        /// limb at its rest pose.
        /// </summary>
        public void SetLimbTarget(BodyPart limb, LimbAngles target)
        {
            List<JointEntry> joints = Joints();
            for (int i = 0; i < joints.Count; i++)
            {
                JointEntry entry = joints[i];
                if (entry.Part != limb) continue;
                float degrees = entry.Kind == JointKind.Elbow || entry.Kind == JointKind.Knee ? target.LowerDegrees : target.UpperDegrees;
                _jointTargets[i] = AnglesFlip ? -degrees : degrees;
            }
        }

        /// <summary>
        /// Pose holding, once per physics step: each powered joint's motor turns toward its target angle (the rest angle unless
        /// <see cref="SetLimbTarget"/> said otherwise) at <paramref name="gainPerSecond"/> degrees per second per degree of
        /// error, within its torque limit.
        /// </summary>
        public void UpdateServos(float gainPerSecond)
        {
            List<JointEntry> joints = Joints();
            for (int i = 0; i < joints.Count; i++)
            {
                HingeJoint2D joint = joints[i].Joint;
                if (!joint.useMotor) continue;
                JointMotor2D motor = joint.motor;
                motor.motorSpeed = -gainPerSecond * (joint.jointAngle - _jointTargets[i]);
                joint.motor = motor;
            }
        }

        /// <summary>
        /// Builds this dummy without a limb it lost in an earlier turn (the limb itself lies in the arena as a severed limb).
        /// Call after <see cref="ApplyPose"/>.
        /// </summary>
        public void LeaveOff(BodyPart part)
        {
            Rigidbody2D lower = GetLowerBody(part);
            if (lower != null) lower.gameObject.SetActive(false);
            Rigidbody2D upper = _upper[(int)part];
            if (upper != null) upper.gameObject.SetActive(false);
        }

        /// <summary>Logic-driven sever: disables the joint that attaches a limb, so it falls and stays on the floor (GDD §12).</summary>
        public void BreakJoint(BodyPart part)
        {
            Rigidbody2D body = _upper[(int)part];
            if (body != null && body.TryGetComponent(out HingeJoint2D joint))
            {
                joint.enabled = false;
            }
        }

        private Rigidbody2D LastSegment(BodyPart arm)
        {
            Rigidbody2D lower = GetLowerBody(arm);
            return lower != null ? lower : _upper[(int)arm];
        }

        /// <summary>Shoulder-to-hand distances the arm can make, in world units: one length for a one-piece arm.</summary>
        private void HandReach(BodyPart arm, out float minReach, out float maxReach)
        {
            Rigidbody2D lower = GetLowerBody(arm);
            float hand = _hands[(int)arm].localPosition.magnitude;
            if (lower == null)
            {
                minReach = maxReach = hand;
                return;
            }

            float upper = Vector2.Distance(lower.transform.localPosition, _upper[(int)arm].transform.localPosition);
            float straightest = _elbowMinDegrees <= 0f && _elbowMaxDegrees >= 0f ? 0f : Mathf.Min(Mathf.Abs(_elbowMinDegrees), Mathf.Abs(_elbowMaxDegrees));
            float mostBent = Mathf.Max(Mathf.Abs(_elbowMinDegrees), Mathf.Abs(_elbowMaxDegrees));
            minReach = ArmReach.DistanceAtBend(upper, hand, mostBent);
            maxReach = ArmReach.DistanceAtBend(upper, hand, straightest);
        }

        /// <summary>A lower segment hanging straight on from its upper segment at a pose, using the built layout.</summary>
        private static BodyPose InLineWith(BodyPose upperPose, Rigidbody2D upper, Rigidbody2D lower, ArenaSpace space)
        {
            Vector2 restOffset = lower.transform.localPosition - upper.transform.localPosition;
            Vec2 offsetUnits = space.ToArena(restOffset).Rotated(upperPose.RotationDegrees);
            return new BodyPose(upperPose.PositionUnits + offsetUnits, upperPose.RotationDegrees);
        }

        private static void SetBody(Rigidbody2D body, BodyPose pose, ArenaSpace space)
        {
            if (body == null) return;
            Vector2 position = space.ToWorld(pose.PositionUnits);
            body.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, 0f, pose.RotationDegrees));
            body.position = position;
            body.rotation = pose.RotationDegrees;
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
        }

        private void SetMass(BodyPart part, float totalMass, float upperShare)
        {
            Rigidbody2D upper = _upper[(int)part];
            if (upper != null) upper.mass = totalMass * upperShare;
            Rigidbody2D lower = GetLowerBody(part);
            if (lower != null) lower.mass = totalMass * (1f - upperShare);
        }

        private void ApplyLimits(JointEntry entry, RagdollSettings body)
        {
            float min, max;
            switch (entry.Kind)
            {
                case JointKind.Neck:
                    min = body.NeckMinDegrees;
                    max = body.NeckMaxDegrees;
                    break;
                case JointKind.Shoulder:
                    min = body.ShoulderMinDegrees;
                    max = body.ShoulderMaxDegrees;
                    break;
                case JointKind.Elbow:
                    min = body.ElbowMinDegrees;
                    max = body.ElbowMaxDegrees;
                    break;
                case JointKind.Hip:
                    min = body.HipMinDegrees;
                    max = body.HipMaxDegrees;
                    break;
                default:
                    min = body.KneeMinDegrees;
                    max = body.KneeMaxDegrees;
                    break;
            }

            entry.Joint.limits = AnglesFlip
                ? new JointAngleLimits2D { min = -max, max = -min }
                : new JointAngleLimits2D { min = min, max = max };
            entry.Joint.useLimits = true;
        }

        /// <summary>
        /// Whether a <see cref="RagdollSettings"/> angle (positive = toward the opponent) is negated as Unity measures it: facing
        /// -X, "forward" turns clockwise; it also flips because Unity measures the other way.
        /// </summary>
        private bool AnglesFlip => IsMirrored == JointAngleGrowsCounterClockwise;

        private List<JointEntry> Joints()
        {
            if (_jointsFound) return _joints;
            _joints.Clear();
            foreach (BodyPart part in BodyParts.All)
            {
                if (part == BodyPart.Torso) continue;
                AddJoint(_upper[(int)part], part, part == BodyPart.Head ? JointKind.Neck : part.IsArm() ? JointKind.Shoulder : JointKind.Hip);
                if (part.IsArm() || part.IsLeg()) AddJoint(GetLowerBody(part), part, part.IsArm() ? JointKind.Elbow : JointKind.Knee);
            }

            _jointTargets = new float[_joints.Count];
            _jointsFound = true;
            return _joints;
        }

        private void AddJoint(Rigidbody2D body, BodyPart part, JointKind kind)
        {
            if (body != null && body.TryGetComponent(out HingeJoint2D joint)) _joints.Add(new JointEntry(joint, body, part, kind));
        }

        private static void Tag(Rigidbody2D body, Side side, BodyRole role, BodyPart part)
        {
            if (body != null && body.TryGetComponent(out PhysicsBodyTag tag)) tag.Configure(side, role, part);
        }

        private static Vector2 DirectionOf(float degrees) => new Vector2(Mathf.Cos(degrees * Mathf.Deg2Rad), Mathf.Sin(degrees * Mathf.Deg2Rad));

        private static Vec2 ToVec2(Vector2 value) => new Vec2(value.x, value.y);

        private static Vector2 MirrorX(Vector2 value) => new Vector2(-value.x, value.y);

        private static Vector3 MirrorX(Vector3 value) => new Vector3(-value.x, value.y, value.z);

        private enum JointKind
        {
            Neck,
            Shoulder,
            Elbow,
            Hip,
            Knee,
        }

        private readonly struct JointEntry
        {
            public HingeJoint2D Joint { get; }
            public Rigidbody2D Body { get; }
            public BodyPart Part { get; }
            public JointKind Kind { get; }

            public JointEntry(HingeJoint2D joint, Rigidbody2D body, BodyPart part, JointKind kind)
            {
                Joint = joint;
                Body = body;
                Part = part;
                Kind = kind;
            }
        }
    }
}
