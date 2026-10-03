using System;
using XRim.Core;
using XRim.Rules;
using XRim.Rules.Settings;
using XRim.Simulation.Drivers;
using XRim.Simulation.Settings;

namespace XRim.Simulation.Physics
{
    /// <summary>
    /// The pose a dummy stands in before its first turn ("en garde"), computed from the settings alone, so a headless
    /// authority, a server and the Unity world all start from the same board. The layout is the placeholder ragdoll's
    /// (<c>PlaceholderRagdollBuilder</c> builds the same pivots from the same settings): pelvis at the torso pose, head on top
    /// of the torso, arms hanging from the shoulder, legs from the pelvis, every limb straight down. With a weapon, the
    /// dominant arm holds it at the guard angle, placed by the aim model so the first step of a swing starts exactly where
    /// the weapon already is, and the hand holds the blade where the arm can reach (<see cref="ArmReach"/>). A shield rests the
    /// same way with its own aim model (held like a shield, A3): its centre at the guard point, its face outward.
    /// </summary>
    public static class GuardStance
    {
        /// <summary>A limb's long axis points down (-90°) in the rest pose, so its rotation is its direction plus this.</summary>
        private const float LimbAxisOffsetDegrees = 90f;

        public static FighterPose Create(BodyPose torso, Side side, BodyPart dominantArm, WeaponStats heldWeapon,
            RagdollSegmentation segmentation, RagdollSettings body, PathSettings paths, IWeaponAimModel aim, IWeaponAimModel shieldAim = null)
        {
            Guard.NotNull(body, nameof(body));
            Guard.NotNull(paths, nameof(paths));
            Guard.NotNull(aim, nameof(aim));
            bool split = segmentation == RagdollSegmentation.TenBodies;
            var pose = new FighterPose { HasLowerSegments = split, HasHeldItem = heldWeapon != null };
            foreach (BodyPart part in BodyParts.All)
            {
                Vec2 pivot = PivotLocal(part, body, paths);
                pose.Set(part, LimbPose(pivot, 0f, torso, side));
                if (split && (part.IsArm() || part.IsLeg()))
                {
                    pose.SetLower(part, LimbPose(pivot - new Vec2(0f, UpperLength(part, body, paths)), 0f, torso, side));
                }
            }

            if (heldWeapon == null) return pose;

            Vec2 shoulder = paths.ShoulderOffsetUnits;
            float armLength = paths.ArmLengthUnits;
            float blade = heldWeapon.LengthUnits;
            IWeaponAimModel itemAim = heldWeapon.Kind == WeaponKind.Shield ? shieldAim ?? new ShieldFaceAimModel() : aim;
            BodyPose grip = itemAim.Aim(TipLocal(heldWeapon, body, paths), shoulder, armLength, blade);
            Vec2 axis = Vec2.FromAngleDegrees(grip.RotationDegrees);
            float upper = UpperLength(dominantArm, body, paths);
            float lower = armLength - upper;
            HandReach(split, upper, lower, body, out float minReach, out float maxReach);
            Vec2 hand = grip.PositionUnits + axis * ArmReach.HandAlongBlade(shoulder, grip.PositionUnits, axis, blade, minReach, maxReach);

            if (split)
            {
                Vec2 elbow = ArmReach.Elbow(shoulder, hand, upper, lower);
                pose.Set(dominantArm, LimbPose(shoulder, LimbRotation(elbow - shoulder), torso, side));
                pose.SetLower(dominantArm, LimbPose(elbow, LimbRotation(hand - elbow), torso, side));
            }
            else
            {
                pose.Set(dominantArm, LimbPose(shoulder, LimbRotation(hand - shoulder), torso, side));
            }

            pose.HeldItem = TorsoFrame.ToArena(grip, torso, side);
            return pose;
        }

        /// <summary>
        /// Where a held item's path point (a weapon's tip, the shield's centre) rests in the guard stance, in the torso frame: a
        /// first stroke's lead-in starts here (D3).
        /// </summary>
        public static Vec2 TipLocal(WeaponStats weapon, RagdollSettings body, PathSettings paths) =>
            paths.ShoulderOffsetUnits + Vec2.FromAngleDegrees(body.GuardAngleDegrees) *
            (paths.ArmLengthUnits * body.GuardHandReachFraction + Guard.NotNull(weapon, nameof(weapon)).LengthUnits);

        /// <summary>Where a part is jointed, in the torso frame as built facing +X.</summary>
        private static Vec2 PivotLocal(BodyPart part, RagdollSettings body, PathSettings paths)
        {
            if (part == BodyPart.Head) return new Vec2(0f, body.TorsoHeightUnits);
            return part.IsArm() ? paths.ShoulderOffsetUnits : Vec2.Zero;
        }

        /// <summary>The length of an arm's or leg's upper segment with ten bodies (where the elbow or knee is).</summary>
        private static float UpperLength(BodyPart part, RagdollSettings body, PathSettings paths)
        {
            float length = part.IsArm() ? paths.ArmLengthUnits : body.LegLengthUnits;
            return length * body.UpperSegmentFraction;
        }

        /// <summary>Shoulder-to-hand distances the arm can make: one length for a one-piece arm, a range with an elbow.</summary>
        private static void HandReach(bool split, float upper, float lower, RagdollSettings body, out float minReach, out float maxReach)
        {
            if (!split)
            {
                minReach = maxReach = upper + lower;
                return;
            }

            float min = body.ElbowMinDegrees;
            float max = body.ElbowMaxDegrees;
            float straightest = min <= 0f && max >= 0f ? 0f : Math.Min(Math.Abs(min), Math.Abs(max));
            float mostBent = Math.Max(Math.Abs(min), Math.Abs(max));
            minReach = ArmReach.DistanceAtBend(upper, lower, mostBent);
            maxReach = ArmReach.DistanceAtBend(upper, lower, straightest);
        }

        /// <summary>A part's pose in the arena from its torso-frame pivot and its rotation as built facing +X.</summary>
        private static BodyPose LimbPose(Vec2 localUnits, float localRotationDegrees, BodyPose torso, Side side) =>
            new BodyPose(TorsoFrame.ToArena(localUnits, torso, side),
                torso.RotationDegrees + (side == Side.Right ? -localRotationDegrees : localRotationDegrees));

        private static float LimbRotation(Vec2 direction) => direction.AngleDegrees + LimbAxisOffsetDegrees;
    }
}
