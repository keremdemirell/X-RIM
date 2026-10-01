using XRim.Config;
using XRim.Core;
using XRim.Rules;
using XRim.Rules.Arena;
using XRim.Rules.Match;
using XRim.Rules.Settings;
using XRim.Simulation;
using XRim.Simulation.Drivers;
using XRim.Simulation.Physics;
using XRim.Simulation.Settings;
using XRim.Simulation.Unity2D;

namespace XRim.DebugTools.Spike
{
    /// <summary>
    /// The feel spike's board: a right-handed attacker on the left in the guard stance and a weaponless target on the right,
    /// both with their pelvis one leg length above the floor, mirrored around x = 0.
    /// </summary>
    internal static class SpikeBoard
    {
        public const Handedness SpikeHandedness = Handedness.Right;

        public static BodyPart DominantArm => BodyParts.DominantArm(SpikeHandedness);

        public static PoseSnapshot CreatePose(Ragdoll template, SimulationSettings simulation, ArenaSpace space, IWeaponAimModel aim,
            WeaponStats attackerWeapon, float distanceUnits)
        {
            float pelvisHeight = simulation.Ragdoll.LegLengthUnits;
            var attacker = new BodyPose(new Vec2(-distanceUnits * 0.5f, pelvisHeight), 0f);
            var target = new BodyPose(new Vec2(distanceUnits * 0.5f, pelvisHeight), 0f);
            return new PoseSnapshot
            {
                Left = template.CreateRestPose(attacker, Side.Left, space, DominantArm, attackerWeapon, simulation.Ragdoll, aim),
                Right = template.CreateRestPose(target, Side.Right, space, DominantArm, null, simulation.Ragdoll, aim),
            };
        }

        public static MatchState CreateState(RulesSettings rules, WeaponId weapon) => new MatchState(
            PerSide<FighterState>.Create(_ => new FighterState(SpikeHandedness, rules.Damage.MaxHp, weapon)),
            PerSide<ElectricWallState>.Create(_ => new ElectricWallState()));

        /// <summary>
        /// A pelvis-to-pelvis distance that puts the target's chest at a share of the weapon's reach (arm plus weapon from
        /// the shoulder, GDD §6), so the weapon can hit it but does not start inside it.
        /// </summary>
        public static float DistanceInsideReach(PathSettings paths, RagdollSettings body, WeaponStats weapon, float reachFraction) =>
            paths.ShoulderOffsetUnits.X + (paths.ArmLengthUnits + weapon.LengthUnits) * reachFraction + body.TorsoWidthUnits * 0.5f;

        /// <summary>True when the attacker's weapon tip already reaches past the target's chest in this pose.</summary>
        public static bool WeaponStartsInsideTarget(PoseSnapshot pose, WeaponStats weapon, RagdollSettings body)
        {
            if (weapon == null || !pose.Left.HasHeldItem) return false;
            BodyPose grip = pose.Left.HeldItem;
            Vec2 tip = grip.PositionUnits + Vec2.FromAngleDegrees(grip.RotationDegrees) * weapon.LengthUnits;
            return tip.X >= pose.Right.Get(BodyPart.Torso).PositionUnits.X - body.TorsoWidthUnits * 0.5f;
        }
    }
}
