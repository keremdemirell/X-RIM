using System;
using System.Collections.Generic;
using UnityEngine;
using XRim.Config;
using XRim.Core;
using XRim.Rules;
using XRim.Simulation.Physics;

namespace XRim.Presentation.Dummy
{
    /// <summary>
    /// The visible dummy. It only copies recorded poses onto its transforms and has no physics of its own.
    /// Cosmetic skins swap what is drawn here; hitboxes live on the physics ragdoll, so skins can never change
    /// hitboxes, silhouette or reach (GDD §2, §16).
    /// Poses are applied in the dummy's local space: the parts are direct children of this object, which sits at the
    /// origin of the arena root, so flipping the arena root mirrors the view (GDD §4) without touching the simulation.
    /// Lost limbs (GDD §11, §12): a limb no longer on the dummy is drawn where it lies on the floor, or hidden while someone wields it
    /// as a club (the club is drawn as the held item); a dropped item is drawn where it falls; a split torso shows its two halves.
    /// </summary>
    public sealed class DummyView : MonoBehaviour
    {
        [Tooltip("One transform per body part, in order: Head, Torso, LeftArm, RightArm, LeftLeg, RightLeg. For split limbs, the upper segment.")]
        [SerializeField] private Transform[] _parts = new Transform[BodyParts.Count];

        [Tooltip("Ten-body dummies only (D2): the lower segment of each arm and leg, in the same order. Empty otherwise.")]
        [SerializeField] private Transform[] _lowerParts = new Transform[BodyParts.Count];

        [Tooltip("The torso's halves, shown instead of the torso once it split (GDD §11). Empty for dummies built before Session 11.")]
        [SerializeField] private Transform _torsoBottom;
        [SerializeField] private Transform _torsoTop;

        [SerializeField] private Transform _heldItem;

        private static readonly IReadOnlyList<SeveredLimbPose> NoFloorLimbs = Array.Empty<SeveredLimbPose>();

        private readonly Dictionary<string, Transform> _heldItemsByWeapon = new Dictionary<string, Transform>();

        /// <summary>Wires a dummy built in code. Arrays are indexed by <see cref="BodyPart"/>; the torso halves may be null.</summary>
        public void Configure(Transform[] parts, Transform[] lowerParts, Transform torsoBottom = null, Transform torsoTop = null)
        {
            _parts = parts;
            _lowerParts = lowerParts;
            _torsoBottom = torsoBottom;
            _torsoTop = torsoTop;
        }

        /// <summary>Registers the drawing of one weapon this dummy can hold; it stays hidden until shown.</summary>
        public void AddHeldItem(WeaponId weapon, Transform visual)
        {
            _heldItemsByWeapon[weapon.Value] = visual;
            visual.gameObject.SetActive(false);
        }

        /// <summary>Shows the weapon the dummy holds now (a weapon switch is public, GDD §6); an unknown weapon shows none.</summary>
        public void ShowWeapon(WeaponId weapon) =>
            SetHeldItem(_heldItemsByWeapon.TryGetValue(weapon.Value ?? string.Empty, out Transform visual) ? visual : null);

        /// <summary>The visual of the item the dummy holds now (it changes with the weapon); null for none.</summary>
        public void SetHeldItem(Transform heldItem)
        {
            if (_heldItem != null && _heldItem != heldItem) _heldItem.gameObject.SetActive(false);
            _heldItem = heldItem;
        }

        /// <summary>Where a part is jointed (its pivot; a split limb's upper segment), in world space.</summary>
        public Vector3 PartPosition(BodyPart part)
        {
            Transform target = _parts[(int)part];
            return target != null ? target.position : transform.position;
        }

        /// <summary>The middle of a part's drawing, in world space.</summary>
        public Vector3 PartCentre(BodyPart part)
        {
            Transform target = _parts[(int)part];
            if (target == null) return transform.position;
            var drawing = target.GetComponentInChildren<Renderer>();
            return drawing != null ? drawing.bounds.center : target.position;
        }

        public void ApplyPose(FighterPose pose, ArenaSpace space) => ApplyPose(pose, Side.Left, NoFloorLimbs, space);

        /// <param name="owner">Which dummy this is: its own severed limbs are the ones in <paramref name="floorLimbs"/> it draws.</param>
        /// <param name="floorLimbs">Every severed limb lying in the arena (<see cref="PoseSnapshot.SeveredLimbs"/>).</param>
        public void ApplyPose(FighterPose pose, Side owner, IReadOnlyList<SeveredLimbPose> floorLimbs, ArenaSpace space)
        {
            Guard.NotNull(pose, nameof(pose));
            Guard.NotNull(floorLimbs, nameof(floorLimbs));
            int count = Mathf.Min(_parts.Length, pose.Parts.Length);
            for (int i = 0; i < count; i++)
            {
                var part = (BodyPart)i;
                Transform lower = pose.HasLowerSegments && _lowerParts != null && i < _lowerParts.Length ? _lowerParts[i] : null;
                if (part.IsSeverable() && pose.IsSevered(part))
                {
                    ApplyLostLimb(part, owner, floorLimbs, _parts[i], lower, space);
                    continue;
                }

                SetVisible(_parts[i], true);
                Apply(_parts[i], pose.Parts[i], space);
                if (lower != null)
                {
                    SetVisible(lower, true);
                    Apply(lower, pose.LowerSegments[i], space);
                }
            }

            ApplyTorso(pose, space);
            ApplyHeldItem(pose, space);
        }

        /// <summary>A limb off the dummy: where it lies on the floor, or hidden while someone wields it (its club is the held item).</summary>
        private static void ApplyLostLimb(BodyPart part, Side owner, IReadOnlyList<SeveredLimbPose> floorLimbs, Transform upper, Transform lower,
            ArenaSpace space)
        {
            for (int i = 0; i < floorLimbs.Count; i++)
            {
                SeveredLimbPose limb = floorLimbs[i];
                if (limb.Owner != owner || limb.Part != part) continue;
                SetVisible(upper, true);
                Apply(upper, limb.Pose, space);
                // A one-piece floor limb drawn by a ten-body dummy (the segmentation changed mid-match) shows its upper segment only.
                SetVisible(lower, limb.HasLower);
                if (limb.HasLower) Apply(lower, limb.Lower, space);
                return;
            }

            SetVisible(upper, false);
            SetVisible(lower, false);
        }

        private void ApplyTorso(FighterPose pose, ArenaSpace space)
        {
            Transform torso = _parts.Length > (int)BodyPart.Torso ? _parts[(int)BodyPart.Torso] : null;
            bool split = pose.IsTorsoSplit && _torsoBottom != null && _torsoTop != null;
            SetVisible(_torsoBottom, split);
            SetVisible(_torsoTop, split);
            if (!split)
            {
                SetVisible(torso, true);
                return;
            }

            SetVisible(torso, false);
            Apply(_torsoBottom, pose.Get(BodyPart.Torso), space);
            Apply(_torsoTop, pose.TorsoTop, space);
        }

        /// <summary>The item in hand, or the one this dummy dropped this turn, falling where it was let go of.</summary>
        private void ApplyHeldItem(FighterPose pose, ArenaSpace space)
        {
            if (_heldItem == null) return;
            bool shown = pose.HasHeldItem || pose.HasDroppedItem;
            _heldItem.gameObject.SetActive(shown);
            if (shown) Apply(_heldItem, pose.HasHeldItem ? pose.HeldItem : pose.DroppedItem, space);
        }

        private static void SetVisible(Transform target, bool visible)
        {
            if (target != null && target.gameObject.activeSelf != visible) target.gameObject.SetActive(visible);
        }

        private static void Apply(Transform target, BodyPose pose, ArenaSpace space)
        {
            if (target == null) return;
            target.SetLocalPositionAndRotation(space.ToWorld(pose.PositionUnits), Quaternion.Euler(0f, 0f, pose.RotationDegrees));
        }
    }
}
