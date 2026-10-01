using System;
using XRim.Core;
using XRim.Rules.Paths;
using XRim.Rules.Settings;
using XRim.Simulation.Physics;

namespace XRim.Simulation.Drivers
{
    /// <summary>
    /// What every path driver shares: the tip is at the path point d = v·t (GDD §9, Decided; speed is the weapon's
    /// stat, never finger speed), the held item is oriented by an <see cref="IWeaponAimModel"/>, and the target moves
    /// with the torso frame (§6). Without a path the weapon is held where it was, in the torso frame, so it travels with a
    /// body move. Subclasses only decide how the held item is brought onto the target.
    /// </summary>
    public abstract class PathWeaponDriver : IWeaponDriver
    {
        private readonly PathSettings _paths;
        private readonly IWeaponAimModel _aim;
        private PathCursor _cursor = new PathCursor(WeaponPath.Empty);
        private Side _side;
        private float _speedUnitsPerSecond;
        private float _weaponLengthUnits;
        private BodyPose? _holdLocal;

        protected PathWeaponDriver(PathSettings paths, IWeaponAimModel aim)
        {
            _paths = Guard.NotNull(paths, nameof(paths));
            _aim = Guard.NotNull(aim, nameof(aim));
        }

        protected bool IsCancelled { get; private set; }

        public void Begin(Side side, WeaponPath path, WeaponStats weapon)
        {
            Guard.NotNull(path, nameof(path));
            Guard.NotNull(weapon, nameof(weapon));
            if (weapon.SpeedUnitsPerSecond <= 0f)
                throw new ArgumentOutOfRangeException(nameof(weapon), weapon.SpeedUnitsPerSecond, "Weapon speed must be positive.");

            _cursor = new PathCursor(path);
            _side = side;
            _speedUnitsPerSecond = weapon.SpeedUnitsPerSecond;
            _weaponLengthUnits = weapon.LengthUnits;
            _holdLocal = null;
            IsCancelled = false;
        }

        public BodyPose EvaluateTarget(SimTime time, BodyPose torso)
        {
            if (_cursor.IsEmpty) return _holdLocal.HasValue ? TorsoFrame.ToArena(_holdLocal.Value, torso, _side) : default;
            Vec2 tip = _cursor.PointAt(DistanceAlongPathUnits(time));
            BodyPose local = _aim.Aim(tip, _paths.ShoulderOffsetUnits, _paths.ArmLengthUnits, _weaponLengthUnits);
            return TorsoFrame.ToArena(local, torso, _side);
        }

        public HeldItemCommand Drive(SimTime stepStart, SimTime stepEnd, BodyPose torsoAtStart, BodyPose torsoAtEnd, BodyState heldItem)
        {
            if (IsCancelled) return HeldItemCommand.Limp;
            if (_cursor.IsEmpty && !_holdLocal.HasValue) _holdLocal = TorsoFrame.ToLocal(heldItem.Pose, torsoAtStart, _side);
            return DriveTowardTarget(stepStart, stepEnd, torsoAtStart, torsoAtEnd, heldItem);
        }

        public float DistanceAlongPathUnits(SimTime time) =>
            XMath.Clamp((float)(time.Seconds * _speedUnitsPerSecond), 0f, _cursor.LengthUnits);

        public bool IsComplete(SimTime time) => IsCancelled || time.Seconds * _speedUnitsPerSecond >= _cursor.LengthUnits;

        public void Cancel() => IsCancelled = true;

        protected abstract HeldItemCommand DriveTowardTarget(SimTime stepStart, SimTime stepEnd, BodyPose torsoAtStart, BodyPose torsoAtEnd,
            BodyState heldItem);
    }
}
