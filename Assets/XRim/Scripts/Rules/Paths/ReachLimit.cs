using XRim.Core;
using XRim.Rules.Settings;

namespace XRim.Rules.Paths
{
    /// <summary>
    /// How far the weapon can reach in the torso frame (GDD §6): a circle around the weapon arm's shoulder whose
    /// radius is arm length plus weapon length. A lunge does not enlarge it: the path moves with the torso, so a
    /// lunge already carries the whole reach forward in the arena (designer, 2026-09-29).
    /// </summary>
    public readonly struct ReachLimit
    {
        /// <summary>Centre of the reach circle in the torso frame, arena units.</summary>
        public Vec2 OriginUnits { get; }

        public float RadiusUnits { get; }

        public ReachLimit(Vec2 originUnits, float radiusUnits)
        {
            OriginUnits = originUnits;
            RadiusUnits = radiusUnits;
        }

        public static ReachLimit For(PathSettings paths, WeaponStats weapon)
        {
            Guard.NotNull(paths, nameof(paths));
            Guard.NotNull(weapon, nameof(weapon));
            return new ReachLimit(paths.ShoulderOffsetUnits, paths.ArmLengthUnits + weapon.LengthUnits);
        }

        public bool Contains(Vec2 point, float toleranceUnits) =>
            Vec2.Distance(point, OriginUnits) <= RadiusUnits + toleranceUnits;
    }
}
