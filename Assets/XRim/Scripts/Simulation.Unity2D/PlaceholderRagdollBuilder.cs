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
    /// Builds the placeholder crash-test dummy in code from primitive shapes (Session 02 feel spike). The Editor menu
    /// XRim/Spike/Build Placeholder Dummies saves the result as prefabs; PlayMode tests build it directly.
    /// Layout, facing +X, with the pelvis at the origin: the torso rises from the pelvis, the head sits on the neck,
    /// both arms hang from the shoulder (<c>PathSettings.ShoulderOffsetUnits</c>) and are <c>ArmLengthUnits</c> long
    /// so the ragdoll's reach matches the §6 reach limit, and both legs hang from the pelvis. With ten bodies (D2) each
    /// arm and leg is split into upper and lower segments that map to the same hit zone.
    /// One held-item body is built per weapon (length × ink thickness, the weapon's mass), inactive until held.
    /// </summary>
    public static class PlaceholderRagdollBuilder
    {
        private const string RootName = "PlaceholderDummy";
        private const string VisualName = "Visual";
        private const string MarkerName = "Marker";
        private const string HandName = "Hand";
        private const string HeldItemPrefix = "HeldItem_";

        // Draw order: far limbs behind the torso, near limbs and the weapon in front. Markers draw over their part.
        private const int FarLimbOrder = -20;
        private const int TorsoOrder = 0;
        private const int HeadOrder = 10;
        private const int NearLimbOrder = 20;
        private const int HeldItemOrder = 30;
        private const int MarkerOrderOffset = 1;

        /// <summary>A marker's size as a share of the part's narrower side.</summary>
        private const float MarkerSizeFraction = 0.8f;

        private static readonly Color DummyColor = new Color(0.93f, 0.9f, 0.82f);
        private static readonly Color BladeColor = new Color(0.75f, 0.78f, 0.82f);
        private static readonly Color HeavyWeaponColor = new Color(0.33f, 0.33f, 0.36f);
        private static readonly Color OtherItemColor = new Color(0.55f, 0.45f, 0.35f);

        public static Ragdoll Build(RagdollBuildSpec spec)
        {
            Guard.NotNull(spec, nameof(spec));
            var context = new BuildContext(spec);
            var root = new GameObject($"{RootName}{(int)spec.Segmentation}");
            var ragdoll = root.AddComponent<Ragdoll>();

            var upper = new Rigidbody2D[BodyParts.Count];
            var lower = new Rigidbody2D[BodyParts.Count];
            var hands = new Transform[BodyParts.Count];
            RagdollSettings body = spec.Body;

            Rigidbody2D torso = context.Box(root.transform, BodyPart.Torso, "Torso", Vector2.zero, body.TorsoWidthUnits,
                body.TorsoHeightUnits, rises: true, body.TorsoMass, TorsoOrder);
            upper[(int)BodyPart.Torso] = torso;

            Rigidbody2D head = context.Head(root.transform, new Vector2(0f, body.TorsoHeightUnits), body.HeadDiameterUnits, body.HeadMass);
            context.Join(head, torso, body.NeckMinDegrees, body.NeckMaxDegrees);
            upper[(int)BodyPart.Head] = head;

            Vector2 shoulder = new Vector2(spec.Paths.ShoulderOffsetUnits.X, spec.Paths.ShoulderOffsetUnits.Y);
            foreach (BodyPart arm in new[] { BodyPart.LeftArm, BodyPart.RightArm })
            {
                Limb limb = context.CreateLimb(root.transform, arm, torso, shoulder, spec.Paths.ArmLengthUnits, body.ArmWidthUnits,
                    body.ArmMass, body.ShoulderMinDegrees, body.ShoulderMaxDegrees, body.ElbowMinDegrees, body.ElbowMaxDegrees);
                upper[(int)arm] = limb.Upper;
                lower[(int)arm] = limb.Lower;
                hands[(int)arm] = context.Hand(limb.Last);
            }

            foreach (BodyPart leg in new[] { BodyPart.LeftLeg, BodyPart.RightLeg })
            {
                Limb limb = context.CreateLimb(root.transform, leg, torso, Vector2.zero, body.LegLengthUnits, body.LegWidthUnits,
                    body.LegMass, body.HipMinDegrees, body.HipMaxDegrees, body.KneeMinDegrees, body.KneeMaxDegrees);
                upper[(int)leg] = limb.Upper;
                lower[(int)leg] = limb.Lower;
            }

            // Held items rest in the right hand, pointing forward; at load the fighter's dominant hand takes the held one.
            Vector2 restGrip = (Vector2)hands[(int)BodyPart.RightArm].parent.localPosition + (Vector2)hands[(int)BodyPart.RightArm].localPosition;
            var heldItems = new List<HeldItemSlot>();
            foreach (WeaponStats weapon in spec.Weapons)
            {
                heldItems.Add(new HeldItemSlot(weapon.Id, context.HeldItem(root.transform, weapon, restGrip)));
            }

            ragdoll.Configure(spec.Segmentation, upper, lower, hands, heldItems, spec.Space, spec.Paths);
            return ragdoll;
        }

        /// <summary>Resizes a held-item body to a weapon's current stats (live tuning of length and ink thickness).</summary>
        public static void FitHeldItem(Rigidbody2D item, WeaponStats weapon, ArenaSpace space)
        {
            float length = space.ToWorldLength(weapon.LengthUnits);
            float width = space.ToWorldLength(weapon.InkThicknessUnits);
            item.mass = weapon.Mass;
            if (item.TryGetComponent(out BoxCollider2D box))
            {
                box.size = new Vector2(length, width);
                box.offset = new Vector2(length * 0.5f, 0f);
            }

            FitHeldItemVisual(item.transform, weapon, space);
        }

        /// <summary>Resizes only the drawing of a held item (for visual copies that have no physics).</summary>
        public static void FitHeldItemVisual(Transform item, WeaponStats weapon, ArenaSpace space)
        {
            float length = space.ToWorldLength(weapon.LengthUnits);
            float width = space.ToWorldLength(weapon.InkThicknessUnits);
            Transform visual = item.Find(VisualName);
            if (visual != null)
            {
                visual.localPosition = new Vector3(length * 0.5f, 0f, 0f);
                visual.localScale = new Vector3(length, width, 1f);
            }
        }

        private readonly struct Limb
        {
            public Rigidbody2D Upper { get; }

            /// <summary>Null with six bodies.</summary>
            public Rigidbody2D Lower { get; }

            public Rigidbody2D Last => Lower != null ? Lower : Upper;

            public Limb(Rigidbody2D upper, Rigidbody2D lower)
            {
                Upper = upper;
                Lower = lower;
            }
        }

        /// <summary>Shapes are laid out in arena units and converted to world units here.</summary>
        private sealed class BuildContext
        {
            private readonly RagdollBuildSpec _spec;

            public BuildContext(RagdollBuildSpec spec)
            {
                _spec = spec;
            }

            private float World(float arenaUnits) => _spec.Space.ToWorldLength(arenaUnits);

            private Vector2 World(Vector2 arenaUnits) => new Vector2(World(arenaUnits.x), World(arenaUnits.y));

            public Limb CreateLimb(Transform root, BodyPart part, Rigidbody2D torso, Vector2 jointUnits, float lengthUnits, float widthUnits,
                float mass, float firstMin, float firstMax, float secondMin, float secondMax)
            {
                bool near = part == BodyPart.RightArm || part == BodyPart.RightLeg;
                int order = near ? NearLimbOrder : FarLimbOrder;
                if (_spec.Segmentation == RagdollSegmentation.SixBodies)
                {
                    Rigidbody2D whole = Box(root, part, part.ToString(), jointUnits, widthUnits, lengthUnits, rises: false, mass, order);
                    Join(whole, torso, firstMin, firstMax);
                    return new Limb(whole, null);
                }

                float share = _spec.Body.UpperSegmentFraction;
                float upperLength = lengthUnits * share;
                Rigidbody2D upper = Box(root, part, part + "Upper", jointUnits, widthUnits, upperLength, rises: false, mass * share, order);
                Join(upper, torso, firstMin, firstMax);
                Rigidbody2D lower = Box(root, part, part + "Lower", jointUnits - new Vector2(0f, upperLength), widthUnits,
                    lengthUnits - upperLength, rises: false, mass * (1f - share), order);
                Join(lower, upper, secondMin, secondMax);
                return new Limb(upper, lower);
            }

            /// <summary>A box body pivoting at <paramref name="pivotUnits"/>, extending up (torso) or down (limbs) from it.</summary>
            public Rigidbody2D Box(Transform root, BodyPart part, string name, Vector2 pivotUnits, float widthUnits, float heightUnits,
                bool rises, float mass, int order)
            {
                Rigidbody2D body = CreateBody(root, name, pivotUnits, mass);
                Vector2 size = World(new Vector2(widthUnits, heightUnits));
                Vector2 centre = new Vector2(0f, (rises ? 0.5f : -0.5f) * size.y);
                var box = body.gameObject.AddComponent<BoxCollider2D>();
                box.size = size;
                box.offset = centre;
                Tag(body, BodyRole.BodyPart, part);
                AddVisual(body.transform, _spec.Sprites?.Square, DummyColor, centre, size, order, true);
                return body;
            }

            public Rigidbody2D Head(Transform root, Vector2 neckUnits, float diameterUnits, float mass)
            {
                Rigidbody2D body = CreateBody(root, "Head", neckUnits, mass);
                float diameter = World(diameterUnits);
                var centre = new Vector2(0f, diameter * 0.5f);
                var circle = body.gameObject.AddComponent<CircleCollider2D>();
                circle.radius = diameter * 0.5f;
                circle.offset = centre;
                Tag(body, BodyRole.BodyPart, BodyPart.Head);
                AddVisual(body.transform, _spec.Sprites?.Circle, DummyColor, centre, new Vector2(diameter, diameter), HeadOrder, true);
                return body;
            }

            public Transform Hand(Rigidbody2D lastSegment)
            {
                var hand = new GameObject(HandName).transform;
                hand.SetParent(lastSegment.transform, false);
                var box = lastSegment.GetComponent<BoxCollider2D>();
                hand.localPosition = new Vector3(0f, box.offset.y * 2f, 0f);
                return hand;
            }

            public Rigidbody2D HeldItem(Transform root, WeaponStats weapon, Vector2 gripWorld)
            {
                var gameObject = new GameObject(HeldItemPrefix + weapon.Id);
                gameObject.transform.SetParent(root, false);
                gameObject.transform.localPosition = gripWorld;
                var body = gameObject.AddComponent<Rigidbody2D>();
                body.useAutoMass = false;
                body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                gameObject.AddComponent<BoxCollider2D>();
                Tag(body, BodyRole.HeldItem, BodyPart.Torso);
                Color color = weapon.Id == WeaponIds.Rapier.Value ? BladeColor : weapon.Id == WeaponIds.Mace.Value ? HeavyWeaponColor : OtherItemColor;
                AddVisual(gameObject.transform, _spec.Sprites?.Square, color, Vector2.zero, Vector2.one, HeldItemOrder, false);
                FitHeldItem(body, weapon, _spec.Space);
                gameObject.SetActive(false);
                return body;
            }

            public void Join(Rigidbody2D child, Rigidbody2D parent, float minDegrees, float maxDegrees)
            {
                var joint = child.gameObject.AddComponent<HingeJoint2D>();
                joint.connectedBody = parent;
                joint.autoConfigureConnectedAnchor = false;
                joint.anchor = Vector2.zero;
                joint.connectedAnchor = child.transform.localPosition - parent.transform.localPosition;
                joint.enableCollision = false;
                joint.useLimits = true;
                joint.limits = new JointAngleLimits2D { min = minDegrees, max = maxDegrees };
                joint.useMotor = false;
            }

            private Rigidbody2D CreateBody(Transform root, string name, Vector2 pivotUnits, float mass)
            {
                var gameObject = new GameObject(name);
                gameObject.transform.SetParent(root, false);
                gameObject.transform.localPosition = World(pivotUnits);
                var body = gameObject.AddComponent<Rigidbody2D>();
                body.useAutoMass = false;
                body.mass = mass;
                return body;
            }

            private static void Tag(Rigidbody2D body, BodyRole role, BodyPart part) =>
                body.gameObject.AddComponent<PhysicsBodyTag>().Configure(null, role, part);

            private void AddVisual(Transform parent, Sprite sprite, Color color, Vector2 centre, Vector2 size, int order, bool withMarker)
            {
                if (sprite == null) return;
                SpriteRenderer shape = CreateRenderer(parent, VisualName, sprite, color, centre, size, order);
                Sprite marker = _spec.Sprites.Marker;
                if (!withMarker || marker == null) return;
                float side = Mathf.Min(size.x, size.y) * MarkerSizeFraction;
                CreateRenderer(parent, MarkerName, marker, Color.white, centre, new Vector2(side, side), shape.sortingOrder + MarkerOrderOffset);
            }

            private static SpriteRenderer CreateRenderer(Transform parent, string name, Sprite sprite, Color color, Vector2 centre, Vector2 size,
                int order)
            {
                var gameObject = new GameObject(name);
                gameObject.transform.SetParent(parent, false);
                gameObject.transform.localPosition = centre;
                gameObject.transform.localScale = new Vector3(size.x, size.y, 1f);
                var renderer = gameObject.AddComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                renderer.color = color;
                renderer.sortingOrder = order;
                return renderer;
            }
        }
    }
}
