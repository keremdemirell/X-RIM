using System.Collections.Generic;
using XRim.Core;
using XRim.Rules;
using XRim.Rules.Settings;
using XRim.Simulation.Settings;

namespace XRim.Simulation.Physics
{
    /// <summary>
    /// The crash-test dummy as the placeholder ragdoll builder lays it out (GDD §2; D2 six or ten bodies), facing +X with the pelvis at the
    /// origin, in arena units: the torso rises from the pelvis, the head sits on the neck, both arms hang from the shoulder
    /// (<see cref="PathSettings.ShoulderOffsetUnits"/>, <see cref="PathSettings.ArmLengthUnits"/> long so the reach matches §6) and both legs
    /// from the pelvis; with ten bodies each arm and leg splits into an upper and a lower segment. The torso's two halves (the split at death,
    /// §11), the joints and the draw order are here too. The right-hand dummy is the same layout mirrored.
    /// Pure: the art spec (Docs/ART_SPEC.md) and the debug hit-zone overlay read it, and a PlayMode test holds the builder to it.
    /// </summary>
    public sealed class DummyLayout
    {
        // Draw order: far limbs behind the torso, near limbs and the weapon in front. Markers draw over their part.
        public const int FarLimbOrder = -20;
        public const int TorsoOrder = 0;
        public const int HeadOrder = 10;
        public const int NearLimbOrder = 20;
        public const int HeldItemOrder = 30;
        public const int MarkerOrderOffset = 1;

        /// <summary>The torso splits at death at this share of its height, from the pelvis up (§11).</summary>
        public const float TorsoHalfFraction = 0.5f;

        /// <summary>A calibration marker's diameter as a share of its body's narrower side.</summary>
        public const float MarkerSizeFraction = 0.8f;

        public const string TorsoName = "Torso";
        public const string TorsoBottomName = "TorsoBottom";
        public const string TorsoTopName = "TorsoTop";
        public const string HeadName = "Head";
        public const string UpperSuffix = "Upper";
        public const string LowerSuffix = "Lower";

        private static readonly BodyPart[] Arms = { BodyPart.LeftArm, BodyPart.RightArm };
        private static readonly BodyPart[] Legs = { BodyPart.LeftLeg, BodyPart.RightLeg };

        private readonly List<DummyBodyLayout> _bodies = new List<DummyBodyLayout>();
        private readonly List<DummyJointLayout> _joints = new List<DummyJointLayout>();

        private DummyLayout(RagdollSegmentation segmentation)
        {
            Segmentation = segmentation;
        }

        public RagdollSegmentation Segmentation { get; }

        /// <summary>Every body, in the builder's order: the torso, its two halves, the head, the arms, the legs.</summary>
        public IReadOnlyList<DummyBodyLayout> Bodies => _bodies;

        /// <summary>Every hinge: the neck, then each arm's and leg's joints from the torso out.</summary>
        public IReadOnlyList<DummyJointLayout> Joints => _joints;

        /// <summary>Where a held item's grip rests, standing: the end of the hanging right arm.</summary>
        public Vec2 RestGripUnits { get; private set; }

        /// <summary>The body with this name, or null.</summary>
        public DummyBodyLayout Find(string name)
        {
            foreach (DummyBodyLayout body in _bodies)
            {
                if (body.Name == name) return body;
            }

            return null;
        }

        /// <summary>The bodies that make up one hit zone: one for the head and torso and a six-body limb, two for a ten-body limb.</summary>
        public IEnumerable<DummyBodyLayout> BodiesOf(BodyPart part)
        {
            foreach (DummyBodyLayout body in _bodies)
            {
                if (body.Part == part && !body.OnlyAfterDeath) yield return body;
            }
        }

        public static DummyLayout Of(RagdollSettings body, PathSettings paths, RagdollSegmentation segmentation)
        {
            Guard.NotNull(body, nameof(body));
            Guard.NotNull(paths, nameof(paths));
            var layout = new DummyLayout(segmentation);

            var torsoSize = new Vec2(body.TorsoWidthUnits, body.TorsoHeightUnits);
            float halfHeight = body.TorsoHeightUnits * TorsoHalfFraction;
            layout.Add(TorsoName, BodyPart.Torso, Vec2.Zero, torsoSize, false, true, TorsoOrder, false);
            layout.Add(TorsoBottomName, BodyPart.Torso, Vec2.Zero, new Vec2(body.TorsoWidthUnits, halfHeight), false, true, TorsoOrder, true);
            layout.Add(TorsoTopName, BodyPart.Torso, new Vec2(0f, halfHeight), new Vec2(body.TorsoWidthUnits, body.TorsoHeightUnits - halfHeight),
                false, true, TorsoOrder, true);

            var neck = new Vec2(0f, body.TorsoHeightUnits);
            layout.Add(HeadName, BodyPart.Head, neck, new Vec2(body.HeadDiameterUnits, body.HeadDiameterUnits), true, true, HeadOrder, false);
            layout._joints.Add(new DummyJointLayout("Neck", HeadName, TorsoName, neck, body.NeckMinDegrees, body.NeckMaxDegrees));

            foreach (BodyPart arm in Arms)
            {
                layout.AddLimb(arm, paths.ShoulderOffsetUnits, paths.ArmLengthUnits, body.ArmWidthUnits, body.UpperSegmentFraction, "Shoulder",
                    body.ShoulderMinDegrees, body.ShoulderMaxDegrees, "Elbow", body.ElbowMinDegrees, body.ElbowMaxDegrees);
            }

            foreach (BodyPart leg in Legs)
            {
                layout.AddLimb(leg, Vec2.Zero, body.LegLengthUnits, body.LegWidthUnits, body.UpperSegmentFraction, "Hip",
                    body.HipMinDegrees, body.HipMaxDegrees, "Knee", body.KneeMinDegrees, body.KneeMaxDegrees);
            }

            layout.RestGripUnits = paths.ShoulderOffsetUnits - new Vec2(0f, paths.ArmLengthUnits);
            return layout;
        }

        /// <summary>The name the builder gives a limb's body: the part alone with six bodies, or the part and its segment with ten.</summary>
        public static string LimbBodyName(BodyPart part, RagdollSegmentation segmentation, bool lower) =>
            segmentation == RagdollSegmentation.SixBodies ? part.ToString() : part + (lower ? LowerSuffix : UpperSuffix);

        /// <summary>The right arm and right leg are on the near side (the dummy faces +X), so they draw in front of the torso.</summary>
        public static bool IsNearSide(BodyPart part) => part == BodyPart.RightArm || part == BodyPart.RightLeg;

        private void Add(string name, BodyPart part, Vec2 pivot, Vec2 size, bool round, bool rises, int order, bool onlyAfterDeath) =>
            _bodies.Add(new DummyBodyLayout(name, part, pivot, size, round, rises, order, onlyAfterDeath));

        private void AddLimb(BodyPart part, Vec2 joint, float length, float width, float upperShare, string firstJoint, float firstMin,
            float firstMax, string secondJoint, float secondMin, float secondMax)
        {
            int order = IsNearSide(part) ? NearLimbOrder : FarLimbOrder;
            string side = part == BodyPart.LeftArm || part == BodyPart.LeftLeg ? "Left" : "Right";
            if (Segmentation == RagdollSegmentation.SixBodies)
            {
                string whole = LimbBodyName(part, Segmentation, false);
                Add(whole, part, joint, new Vec2(width, length), false, false, order, false);
                _joints.Add(new DummyJointLayout(side + firstJoint, whole, TorsoName, joint, firstMin, firstMax));
                return;
            }

            float upperLength = length * upperShare;
            string upper = LimbBodyName(part, Segmentation, false);
            string lower = LimbBodyName(part, Segmentation, true);
            Vec2 bend = joint - new Vec2(0f, upperLength);
            Add(upper, part, joint, new Vec2(width, upperLength), false, false, order, false);
            Add(lower, part, bend, new Vec2(width, length - upperLength), false, false, order, false);
            _joints.Add(new DummyJointLayout(side + firstJoint, upper, TorsoName, joint, firstMin, firstMax));
            _joints.Add(new DummyJointLayout(side + secondJoint, lower, upper, bend, secondMin, secondMax));
        }
    }
}
