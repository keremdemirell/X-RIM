using System.Collections.Generic;
using XRim.Core;
using XRim.Rules;
using XRim.Rules.Match;
using XRim.Rules.Settings;
using XRim.Simulation.Physics;

namespace XRim.Tests.EditMode.Simulation
{
    /// <summary>
    /// Scriptable stand-in for Unity physics. Tests queue the contacts a step should produce, so the execution
    /// loop's priority, interrupt and clash handling can be tested without a physics engine.
    /// </summary>
    internal sealed class FakePhysicsWorld : IPhysicsWorld
    {
        private readonly Queue<List<ContactFacts>> _scriptedContacts = new Queue<List<ContactFacts>>();
        private readonly List<ContactFacts> _lastStepContacts = new List<ContactFacts>();
        private PoseSnapshot _pose = new PoseSnapshot();

        public int StepCount { get; private set; }
        public List<(Side Side, BodyPart Part)> BrokenJoints { get; } = new List<(Side, BodyPart)>();

        /// <summary>Contacts to report after the next call to Step (one list per step).</summary>
        public void QueueContactsForNextStep(params ContactFacts[] contacts) => _scriptedContacts.Enqueue(new List<ContactFacts>(contacts));

        public void Load(PoseSnapshot pose, MatchState state, RulesSettings settings) => _pose = pose.Clone();

        public void SetHeldItemTarget(Side side, BodyPose target) => _pose.Get(side).HeldItem = target;

        public void SetRootTarget(Side side, BodyPose target) => _pose.Get(side).Set(BodyPart.Torso, target);

        public void Step(float deltaSeconds)
        {
            StepCount++;
            _lastStepContacts.Clear();
            if (_scriptedContacts.Count > 0) _lastStepContacts.AddRange(_scriptedContacts.Dequeue());
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

        public bool IsSettled(float linearSpeedUnitsPerSecond, float angularSpeedDegreesPerSecond) => true;

        public void Dispose()
        {
        }
    }
}
