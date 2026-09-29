using System;
using System.Collections.Generic;
using UnityEngine;
using XRim.Config;
using XRim.Core;
using XRim.Rules;
using XRim.Rules.Settings;
using XRim.Simulation.Drivers;
using XRim.Simulation.Physics;
using XRim.Simulation.Settings;

namespace XRim.Simulation.Unity2D
{
    /// <summary>
    /// A dummy's physics body: Rigidbody2D parts joined by HingeJoint2D (GDD §18), one body per <see cref="BodyPart"/>
    /// or, with ten bodies (D2), upper and lower segments of each arm and leg that belong to the same hit zone.
    /// Every body is a direct child of this object, whose origin is the pelvis (the torso frame's origin).
    /// The prefab is built facing +X; <see cref="MirrorForRightSide"/> turns an instance to face -X.
    /// Severing is driven by game logic (limb durability reached 0), never by HingeJoint2D.breakForce, which the GDD
    /// rejects because physics-driven breaks are hard to balance (§18).
    /// </summary>
    public sealed class Ragdoll : MonoBehaviour
    {
        /// <summary>How far the built reach may differ from the current path settings before it counts as stale.</summary>
        private const float ReachToleranceUnits = 0.5f;

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
        private bool _jointsFound;

        public RagdollSegmentation Segmentation => _segmentation;
        public bool HasLowerSegments => _segmentation == RagdollSegmentation.TenBodies;
        public bool IsMirrored { get; private set; }
        public IReadOnlyList<HeldItemSlot> HeldItems => _heldItems;

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
        /// reach no longer matches the §6 reach limit. Rebuild it with XRim/Spike/Build Placeholder Dummies.
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
        /// Turns this instance to face -X (the right-hand fighter). Call once, before applying a pose or tuning.
        /// Every placeholder shape is symmetric about its body's vertical axis, so only positions, anchors and joint
        /// limits change.
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
        /// Applies the live masses, joint limits and pose-holding strength (they can change between turns without a
        /// rebuild), and gravity. <paramref name="limpArm"/> is the arm the held weapon carries: its motors are off unless
        /// <see cref="RagdollSettings.ServoWeaponArm"/> is set, so they never fight the weapon's path.
        /// </summary>
        public void ApplyTuning(RagdollSettings body, float gravityScale, BodyPart? limpArm)
        {
            Guard.NotNull(body, nameof(body));
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
        }

        /// <summary>
        /// Pose holding, once per physics step: each powered joint's motor turns back toward its rest angle at
        /// <paramref name="gainPerSecond"/> degrees per second per degree of bend, within its torque limit.
        /// </summary>
        public void UpdateServos(float gainPerSecond)
        {
            foreach (JointEntry entry in Joints())
            {
                if (!entry.Joint.useMotor) continue;
                JointMotor2D motor = entry.Joint.motor;
                motor.motorSpeed = -gainPerSecond * entry.Joint.jointAngle;
                entry.Joint.motor = motor;
            }
        }

        /// <summary>
        /// The standing pose, in arena units: the built layout placed at <paramref name="torso"/> (the pelvis), facing the
        /// opponent, with the held item gripped in the dominant hand and pointing forward (GDD §12).
        /// </summary>
        public FighterPose CreateRestPose(BodyPose torso, Side side, ArenaSpace space, BodyPart dominantArm, bool holdsItem)
        {
            var pose = new FighterPose { HasLowerSegments = HasLowerSegments, HasHeldItem = holdsItem };
            foreach (BodyPart part in BodyParts.All)
            {
                pose.Set(part, RestPoseOf(_upper[(int)part], torso, side, space));
                Rigidbody2D lower = GetLowerBody(part);
                if (lower != null) pose.SetLower(part, RestPoseOf(lower, torso, side, space));
            }

            Transform hand = _hands[(int)dominantArm];
            if (hand != null)
            {
                Vector2 gripWorld = UnmirroredLocal(hand.parent) + UnmirroredLocal(hand);
                pose.HeldItem = TorsoFrame.ToArena(new BodyPose(space.ToArena(gripWorld), 0f), torso, side);
            }

            return pose;
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

        private BodyPose RestPoseOf(Rigidbody2D body, BodyPose torso, Side side, ArenaSpace space)
        {
            if (body == null) return torso;
            // Rest rotations are 0 and every part is symmetric about its vertical axis, so mirroring keeps rotation 0.
            return new BodyPose(TorsoFrame.ToArena(space.ToArena(UnmirroredLocal(body.transform)), torso, side), torso.RotationDegrees);
        }

        private Vector2 UnmirroredLocal(Transform target)
        {
            Vector2 local = target.localPosition;
            return IsMirrored ? MirrorX(local) : local;
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

            // Facing -X, "forward" turns clockwise, so the limits flip.
            entry.Joint.limits = IsMirrored
                ? new JointAngleLimits2D { min = -max, max = -min }
                : new JointAngleLimits2D { min = min, max = max };
            entry.Joint.useLimits = true;
        }

        private List<JointEntry> Joints()
        {
            if (_jointsFound) return _joints;
            _joints.Clear();
            foreach (BodyPart part in BodyParts.All)
            {
                if (part == BodyPart.Torso) continue;
                bool limb = part.IsArm() || part.IsLeg();
                AddJoint(_upper[(int)part], part, part == BodyPart.Head ? JointKind.Neck : part.IsArm() ? JointKind.Shoulder : JointKind.Hip);
                if (limb) AddJoint(GetLowerBody(part), part, part.IsArm() ? JointKind.Elbow : JointKind.Knee);
            }

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
