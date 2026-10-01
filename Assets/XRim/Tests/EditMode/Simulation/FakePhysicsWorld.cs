using System.Collections.Generic;
using XRim.Core;
using XRim.Rules;
using XRim.Rules.Arena;
using XRim.Rules.Match;
using XRim.Rules.Settings;
using XRim.Simulation;
using XRim.Simulation.Physics;

namespace XRim.Tests.EditMode.Simulation
{
    /// <summary>
    /// Scriptable stand-in for Unity physics. Tests queue the contacts a step should produce, so the execution
    /// loop's priority, interrupt and clash handling can be tested without a physics engine. Held items obey their
    /// commands exactly: a move lands on its target at the end of the step, a push is integrated like Box2D does
    /// (semi-implicit Euler). Nothing collides; the root target becomes the torso pose. Physics counts as settled from
    /// <see cref="SettlesAtStep"/> on.
    /// </summary>
    internal sealed class FakePhysicsWorld : IPhysicsWorld
    {
        private const int SideCount = 2;

        private readonly Queue<List<ContactFacts>> _scriptedContacts = new Queue<List<ContactFacts>>();
        private readonly Dictionary<int, List<ContactFacts>> _scheduledContacts = new Dictionary<int, List<ContactFacts>>();
        private readonly List<ContactFacts> _lastStepContacts = new List<ContactFacts>();
        private readonly BodyPose?[] _pendingMove = new BodyPose?[SideCount];
        private readonly Vec2[] _pendingAcceleration = new Vec2[SideCount];
        private readonly float[] _pendingAngularAcceleration = new float[SideCount];
        private readonly Vec2[] _heldVelocity = new Vec2[SideCount];
        private readonly float[] _heldAngularVelocity = new float[SideCount];
        private PoseSnapshot _pose = new PoseSnapshot();

        public int StepCount { get; private set; }
        public int LoadCount { get; private set; }
        public SimulationSettings LoadedSimulation { get; private set; }
        public PoseSnapshot LoadedPose { get; private set; }
        public ArenaEdges LoadedEdges { get; private set; }

        /// <summary>Every root target set, in order (side, pose).</summary>
        public List<(Side Side, BodyPose Pose)> RootTargets { get; } = new List<(Side, BodyPose)>();

        /// <summary>The first step count at which <see cref="IsSettled"/> is true (0 = always settled, int.MaxValue = never).</summary>
        public int SettlesAtStep { get; set; }
        public List<(Side Side, BodyPart Part)> BrokenJoints { get; } = new List<(Side, BodyPart)>();
        public float TouchDistanceUnits { get; set; }

        /// <summary>Contacts to report after the next call to Step (one list per step).</summary>
        public void QueueContactsForNextStep(params ContactFacts[] contacts) => _scriptedContacts.Enqueue(new List<ContactFacts>(contacts));

        /// <summary>Contacts to report after a given call to Step (1 = the first step after Load).</summary>
        public void ScheduleContacts(int stepNumber, params ContactFacts[] contacts)
        {
            if (!_scheduledContacts.TryGetValue(stepNumber, out List<ContactFacts> list))
            {
                list = new List<ContactFacts>();
                _scheduledContacts[stepNumber] = list;
            }

            list.AddRange(contacts);
        }

        public void Load(PoseSnapshot pose, MatchState state, RulesSettings rules, SimulationSettings simulation, ArenaEdges edges)
        {
            _pose = pose.Clone();
            LoadedPose = pose;
            LoadedEdges = edges;
            RootTargets.Clear();
            LoadedSimulation = simulation;
            LoadCount++;
            StepCount = 0;
            for (int i = 0; i < SideCount; i++)
            {
                _pendingMove[i] = null;
                _pendingAcceleration[i] = Vec2.Zero;
                _pendingAngularAcceleration[i] = 0f;
                _heldVelocity[i] = Vec2.Zero;
                _heldAngularVelocity[i] = 0f;
            }
        }

        public void SetHeldItemTarget(Side side, BodyPose target) => _pendingMove[(int)side] = target;

        public void PushHeldItem(Side side, Vec2 accelerationUnitsPerSecondSquared, float angularAccelerationDegreesPerSecondSquared)
        {
            _pendingMove[(int)side] = null;
            _pendingAcceleration[(int)side] = accelerationUnitsPerSecondSquared;
            _pendingAngularAcceleration[(int)side] = angularAccelerationDegreesPerSecondSquared;
        }

        public BodyState GetHeldItemState(Side side) =>
            new BodyState(_pose.Get(side).HeldItem, _heldVelocity[(int)side], _heldAngularVelocity[(int)side]);

        public void SetRootTarget(Side side, BodyPose target)
        {
            RootTargets.Add((side, target));
            _pose.Get(side).Set(BodyPart.Torso, target);
        }

        public void Step(float deltaSeconds)
        {
            MoveHeldItem(Side.Left, deltaSeconds);
            MoveHeldItem(Side.Right, deltaSeconds);

            StepCount++;
            _lastStepContacts.Clear();
            if (_scriptedContacts.Count > 0) _lastStepContacts.AddRange(_scriptedContacts.Dequeue());
            if (_scheduledContacts.TryGetValue(StepCount, out List<ContactFacts> scheduled)) _lastStepContacts.AddRange(scheduled);
        }

        public void DrainContacts(List<ContactFacts> into)
        {
            into.AddRange(_lastStepContacts);
            _lastStepContacts.Clear();
        }

        public void BreakJoint(Side side, BodyPart part) => BrokenJoints.Add((side, part));

        public void DropHeldItem(Side side) => _pose.Get(side).HasHeldItem = false;

        public void ApplyImpulse(Side side, BodyPart part, Vec2 impulse)
        {
        }

        public BodyPose GetPose(Side side, BodyPart part) => _pose.Get(side).Get(part);

        public PoseSnapshot CapturePose() => _pose.Clone();

        public bool IsSettled(float linearSpeedUnitsPerSecond, float angularSpeedDegreesPerSecond) => StepCount >= SettlesAtStep;

        public void Dispose()
        {
        }

        private void MoveHeldItem(Side side, float deltaSeconds)
        {
            int index = (int)side;
            FighterPose fighter = _pose.Get(side);
            BodyPose held = fighter.HeldItem;
            if (_pendingMove[index].HasValue)
            {
                BodyPose target = _pendingMove[index].Value;
                _heldVelocity[index] = (target.PositionUnits - held.PositionUnits) / deltaSeconds;
                _heldAngularVelocity[index] = XMath.DeltaAngleDegrees(held.RotationDegrees, target.RotationDegrees) / deltaSeconds;
                fighter.HeldItem = target;
                _pendingMove[index] = null;
                return;
            }

            _heldVelocity[index] += _pendingAcceleration[index] * deltaSeconds;
            _heldAngularVelocity[index] += _pendingAngularAcceleration[index] * deltaSeconds;
            fighter.HeldItem = new BodyPose(held.PositionUnits + _heldVelocity[index] * deltaSeconds,
                held.RotationDegrees + _heldAngularVelocity[index] * deltaSeconds);
            _pendingAcceleration[index] = Vec2.Zero;
            _pendingAngularAcceleration[index] = 0f;
        }
    }
}
