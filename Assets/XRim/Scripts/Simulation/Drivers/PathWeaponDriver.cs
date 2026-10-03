using System;
using System.Collections.Generic;
using XRim.Core;
using XRim.Rules.Paths;
using XRim.Rules.Settings;
using XRim.Simulation.Physics;
using XRim.Simulation.Settings;

namespace XRim.Simulation.Drivers
{
    /// <summary>
    /// What every path driver shares: the tip is at the path point d = v·t (GDD §9, Decided; speed is the weapon's
    /// stat, never finger speed), the held item is oriented by an <see cref="IWeaponAimModel"/>, and the target moves
    /// with the torso frame (§6). Without a path the weapon is held where it was, in the torso frame, so it travels with a
    /// body move. Subclasses only decide how the held item is brought onto the target.
    /// <para>
    /// The hit rules change the pace: a slowing hit (D26) lowers the speed from its time on, and a stop freezes the distance.
    /// After a stop the weapon is handed to physics if it was moved kinematically (D1: it carries
    /// <see cref="HitReactionSettings.ReleaseSpeedFraction"/> of its speed into the hit), then the motor
    /// (<see cref="WeaponMotorControl"/>) holds it at the stop point in the torso frame, backed off along the path by
    /// <see cref="HitReactionSettings.RecoilDistanceUnits"/> after a last hit or a rebound.
    /// </para>
    /// <para>
    /// The clash and block rules (§7, §10; A6) stop it two more ways. A rebound hands it to physics bouncing back at
    /// <see cref="ClashReactionSettings.ReboundSpeedFraction"/> of its speed. A knock-off hands it to physics with its own motion
    /// along the knock removed (the impact stopped it there) and the knock added, lets it fly free for
    /// <see cref="ClashReactionSettings.KnockOffFreeSeconds"/>, then holds it wherever it ended up, off its path.
    /// </para>
    /// </summary>
    public abstract class PathWeaponDriver : IWeaponDriver
    {
        private readonly PathSettings _paths;
        private readonly IWeaponAimModel _aim;
        private readonly WeaponMotorSettings _holdMotor;
        private readonly HitReactionSettings _reaction;
        private readonly ClashReactionSettings _clashReaction;
        private readonly List<Pace> _paces = new List<Pace>();
        private PathCursor _cursor = new PathCursor(WeaponPath.Empty);
        private Side _side;
        private float _speedUnitsPerSecond;
        private float _weaponLengthUnits;
        private BodyPose? _holdLocal;
        private SimTime? _stopTime;
        private BodyPose? _stopHoldLocal;
        private bool _releasePending;
        private bool _stoppedWhileKinematic;
        private WeaponStopKind _stopKind;
        private Vec2 _knockVelocity;
        private bool _lastCommandMoved;
        private Sample _lastMove;
        private Sample _previousMove;

        protected PathWeaponDriver(PathSettings paths, IWeaponAimModel aim, WeaponMotorSettings holdMotor, HitReactionSettings reaction,
            ClashReactionSettings clashReaction)
        {
            _paths = Guard.NotNull(paths, nameof(paths));
            _aim = Guard.NotNull(aim, nameof(aim));
            _holdMotor = Guard.NotNull(holdMotor, nameof(holdMotor));
            _reaction = Guard.NotNull(reaction, nameof(reaction));
            _clashReaction = Guard.NotNull(clashReaction, nameof(clashReaction));
        }

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
            _paces.Clear();
            _paces.Add(new Pace(SimTime.Zero, 0f, _speedUnitsPerSecond));
            _stopTime = null;
            _stopHoldLocal = null;
            _releasePending = false;
            _stoppedWhileKinematic = false;
            _stopKind = default;
            _knockVelocity = Vec2.Zero;
            _lastCommandMoved = false;
            _lastMove = default;
            _previousMove = default;
        }

        public BodyPose EvaluateTarget(SimTime time, BodyPose torso)
        {
            if (IsStoppedAt(time) && _stopHoldLocal.HasValue) return TorsoFrame.ToArena(_stopHoldLocal.Value, torso, _side);
            if (_cursor.IsEmpty) return _holdLocal.HasValue ? TorsoFrame.ToArena(_holdLocal.Value, torso, _side) : default;
            return TorsoFrame.ToArena(LocalAt(DistanceAlongPathUnits(time)), torso, _side);
        }

        public HeldItemCommand Drive(SimTime stepStart, SimTime stepEnd, BodyPose torsoAtStart, BodyPose torsoAtEnd, BodyState heldItem)
        {
            if (IsStoppedAt(stepStart)) return DriveStopped(stepStart, stepEnd, torsoAtStart, torsoAtEnd, heldItem);

            if (_cursor.IsEmpty && !_holdLocal.HasValue) _holdLocal = TorsoFrame.ToLocal(heldItem.Pose, torsoAtStart, _side);
            HeldItemCommand command = DriveTowardTarget(stepStart, stepEnd, torsoAtStart, torsoAtEnd, heldItem);
            _lastCommandMoved = command.Kind == HeldItemCommandKind.MoveTo;
            if (_lastCommandMoved)
            {
                _previousMove = _lastMove;
                _lastMove = new Sample(command.Target, stepEnd);
            }

            return command;
        }

        public float DistanceAlongPathUnits(SimTime time)
        {
            Pace pace = _paces[0];
            for (int i = _paces.Count - 1; i > 0; i--)
            {
                if (_paces[i].Time > time) continue;
                pace = _paces[i];
                break;
            }

            float distance = pace.DistanceUnits + (float)((time - pace.Time).Seconds * pace.SpeedUnitsPerSecond);
            return XMath.Clamp(distance, 0f, _cursor.LengthUnits);
        }

        public bool IsComplete(SimTime time) => IsStoppedAt(time) || DistanceAlongPathUnits(time) >= _cursor.LengthUnits;

        public bool IsTravelling(SimTime time) =>
            !_cursor.IsEmpty && !IsStoppedAt(time) && DistanceAlongPathUnits(time) < _cursor.LengthUnits;

        public void SlowTo(SimTime time, float speedFraction)
        {
            if (_stopTime.HasValue) return;
            time = NotBeforeLastChange(time);
            _paces.Add(new Pace(time, DistanceAlongPathUnits(time), _speedUnitsPerSecond * XMath.Clamp01(speedFraction)));
        }

        public void Stop(SimTime time, WeaponStopKind kind, Vec2 knockVelocityUnitsPerSecond = default)
        {
            if (_stopTime.HasValue) return;
            time = NotBeforeLastChange(time);
            float distance = DistanceAlongPathUnits(time);
            _paces.Add(new Pace(time, distance, 0f));
            _stopTime = time;
            _stopKind = kind;
            _knockVelocity = kind == WeaponStopKind.KnockedOff ? knockVelocityUnitsPerSecond : Vec2.Zero;
            bool recoils = kind == WeaponStopKind.LastHit || kind == WeaponStopKind.Rebounded;
            float recoil = recoils ? _reaction.RecoilDistanceUnits : 0f;
            // A knocked-off weapon is held where its flight ends, not on its path.
            _stopHoldLocal = kind == WeaponStopKind.KnockedOff ? (BodyPose?)null
                : _cursor.IsEmpty ? _holdLocal : LocalAt(Math.Max(0f, distance - recoil));
            _stoppedWhileKinematic = _lastCommandMoved;
            _releasePending = _lastCommandMoved || kind == WeaponStopKind.KnockedOff || kind == WeaponStopKind.Rebounded;
        }

        protected abstract HeldItemCommand DriveTowardTarget(SimTime stepStart, SimTime stepEnd, BodyPose torsoAtStart, BodyPose torsoAtEnd,
            BodyState heldItem);

        private bool IsStoppedAt(SimTime time) => _stopTime.HasValue && time >= _stopTime.Value;

        private SimTime NotBeforeLastChange(SimTime time)
        {
            SimTime last = _paces[_paces.Count - 1].Time;
            return time < last ? last : time;
        }

        private BodyPose LocalAt(float distanceUnits) =>
            _aim.Aim(_cursor.PointAt(distanceUnits), _paths.ShoulderOffsetUnits, _paths.ArmLengthUnits, _weaponLengthUnits);

        /// <summary>
        /// The first step after a stop hands the blade to physics (a kinematic blade always; any blade that rebounds or is
        /// knocked off); a knocked-off blade then flies free for a moment; every later step holds it with the motor.
        /// </summary>
        private HeldItemCommand DriveStopped(SimTime stepStart, SimTime stepEnd, BodyPose torsoAtStart, BodyPose torsoAtEnd, BodyState heldItem)
        {
            _lastCommandMoved = false;
            bool knockedOff = _stopKind == WeaponStopKind.KnockedOff;
            if (!_stopHoldLocal.HasValue && !knockedOff) _stopHoldLocal = TorsoFrame.ToLocal(heldItem.Pose, torsoAtStart, _side);
            if (_releasePending)
            {
                _releasePending = false;
                return Release(heldItem);
            }

            if (!_stopHoldLocal.HasValue)
            {
                if (IsFlyingFree(stepStart)) return HeldItemCommand.Limp;
                _stopHoldLocal = TorsoFrame.ToLocal(heldItem.Pose, torsoAtStart, _side);
            }

            return WeaponMotorControl.Toward(EvaluateTarget(stepStart, torsoAtStart), EvaluateTarget(stepEnd, torsoAtEnd),
                (float)(stepEnd - stepStart).Seconds, heldItem, _holdMotor);
        }

        private bool IsFlyingFree(SimTime time) =>
            _stopKind == WeaponStopKind.KnockedOff && (time - _stopTime.Value).Seconds < _clashReaction.KnockOffFreeSeconds;

        /// <summary>
        /// The blade as a free body: its own motion (the last kinematic moves, or the free body's velocity) carried at the release
        /// share; bounced back for a rebound; for a knock-off, with its motion along the knock removed and the knock added.
        /// </summary>
        private HeldItemCommand Release(BodyState heldItem)
        {
            OwnMotion(heldItem, out Vec2 velocity, out float angularVelocity);
            switch (_stopKind)
            {
                case WeaponStopKind.Rebounded:
                    float back = -_clashReaction.ReboundSpeedFraction;
                    return HeldItemCommand.Release(velocity * back, angularVelocity * back);
                case WeaponStopKind.KnockedOff:
                    float share = _reaction.ReleaseSpeedFraction;
                    Vec2 carried = velocity * share;
                    if (_knockVelocity != Vec2.Zero)
                    {
                        Vec2 knock = _knockVelocity.Normalized;
                        carried -= knock * Vec2.Dot(carried, knock);
                    }

                    return HeldItemCommand.Release(carried + _knockVelocity, angularVelocity * share);
                default:
                    return HeldItemCommand.Release(velocity * _reaction.ReleaseSpeedFraction, angularVelocity * _reaction.ReleaseSpeedFraction);
            }
        }

        /// <summary>How the blade was moving when it was stopped: from its last two kinematic moves, or the free body's own velocity.</summary>
        private void OwnMotion(BodyState heldItem, out Vec2 velocity, out float angularVelocity)
        {
            if (!_stoppedWhileKinematic)
            {
                velocity = heldItem.LinearVelocityUnitsPerSecond;
                angularVelocity = heldItem.AngularVelocityDegreesPerSecond;
                return;
            }

            float seconds = _previousMove.IsSet ? (float)(_lastMove.Time - _previousMove.Time).Seconds : 0f;
            if (seconds <= 0f)
            {
                velocity = Vec2.Zero;
                angularVelocity = 0f;
                return;
            }

            velocity = (_lastMove.Pose.PositionUnits - _previousMove.Pose.PositionUnits) / seconds;
            angularVelocity = XMath.DeltaAngleDegrees(_previousMove.Pose.RotationDegrees, _lastMove.Pose.RotationDegrees) / seconds;
        }

        /// <summary>From <see cref="Time"/> on, the weapon travels at <see cref="SpeedUnitsPerSecond"/> from <see cref="DistanceUnits"/>.</summary>
        private readonly struct Pace
        {
            public SimTime Time { get; }
            public float DistanceUnits { get; }
            public float SpeedUnitsPerSecond { get; }

            public Pace(SimTime time, float distanceUnits, float speedUnitsPerSecond)
            {
                Time = time;
                DistanceUnits = distanceUnits;
                SpeedUnitsPerSecond = speedUnitsPerSecond;
            }
        }

        /// <summary>A pose the held item was moved onto, and when it got there.</summary>
        private readonly struct Sample
        {
            public BodyPose Pose { get; }
            public SimTime Time { get; }
            public bool IsSet { get; }

            public Sample(BodyPose pose, SimTime time)
            {
                Pose = pose;
                Time = time;
                IsSet = true;
            }
        }
    }
}
