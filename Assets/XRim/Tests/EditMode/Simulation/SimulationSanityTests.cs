using System.Collections.Generic;
using NUnit.Framework;
using XRim.Core;
using XRim.Rules;
using XRim.Rules.Events;
using XRim.Simulation;
using XRim.Simulation.Physics;
using XRim.Simulation.Recording;

namespace XRim.Tests.EditMode.Simulation
{
    public sealed class SimulationSanityTests
    {
        [Test]
        public void SimClock_ComputesTimeFromStepIndex_WithoutDrift()
        {
            var clock = new SimClock(240);
            Assert.That(clock.TimeAtStep(240), Is.EqualTo(SimTime.FromSeconds(1.0)));
            Assert.That(clock.TimeAtStep(2400), Is.EqualTo(SimTime.FromSeconds(10.0)));
            Assert.That(clock.StepsFor(1.5), Is.EqualTo(360), "1.5 s hard cap at 240 Hz");
        }

        [Test]
        public void TimelineRecorder_KeepsFramesAndEventsInOrder()
        {
            var recorder = new TimelineRecorder();
            var clock = new SimClock(240);
            for (int step = 0; step < 3; step++)
            {
                recorder.RecordFrame(step, clock.TimeAtStep(step), new PoseSnapshot());
            }

            recorder.RecordEvent(new LimbSeveredEvent(clock.TimeAtStep(2), Side.Right, BodyPart.LeftArm));
            TurnTimeline timeline = recorder.Build();

            Assert.That(timeline.Frames.Count, Is.EqualTo(3));
            Assert.That(timeline.Duration, Is.EqualTo(clock.TimeAtStep(2)));
            Assert.That(timeline.Events[0], Is.InstanceOf<LimbSeveredEvent>());
        }

        [Test]
        public void FakePhysicsWorld_ReportsScriptedContactsOnce()
        {
            using (var world = new FakePhysicsWorld())
            {
                var tag = new BodyTag(Side.Left, BodyRole.HeldItem, BodyPart.Torso);
                world.QueueContactsForNextStep(new ContactFacts(tag, tag, Vec2.Zero, Vec2.UnitX, Vec2.Zero));

                var contacts = new List<ContactFacts>();
                world.Step(1f / 240f);
                world.DrainContacts(contacts);
                world.Step(1f / 240f);
                world.DrainContacts(contacts);

                Assert.That(world.StepCount, Is.EqualTo(2));
                Assert.That(contacts.Count, Is.EqualTo(1));
            }
        }
    }
}
