using NUnit.Framework;
using XRim.Core;
using XRim.Rules;
using XRim.Simulation.Execution;
using XRim.Simulation.Physics;
using XRim.Simulation.Recording;

namespace XRim.Tests.EditMode.Simulation.Recording
{
    public sealed class TimelineRecorderTests
    {
        private static ContactEvent At(long microseconds, int reportedStep)
        {
            var tag = new BodyTag(Side.Left, BodyRole.BodyPart, BodyPart.Torso);
            return new ContactEvent(new TurnContact(new ContactFacts(tag, tag, Vec2.Zero, Vec2.UnitX, Vec2.Zero), reportedStep,
                new SimTime(microseconds), null, 0f, 0f));
        }

        [Test]
        public void Events_ComeOutInTimeOrder_EqualTimesInRecordedOrder()
        {
            // A contact refined into an earlier step can be recorded after one that happened later.
            var recorder = new TimelineRecorder();
            recorder.RecordEvent(At(8_000, 1));
            recorder.RecordEvent(At(4_000, 2));
            recorder.RecordEvent(At(8_000, 3));

            TurnTimeline timeline = recorder.Build();

            Assert.That(((ContactEvent)timeline.Events[0]).Contact.ReportedStep, Is.EqualTo(2));
            Assert.That(((ContactEvent)timeline.Events[1]).Contact.ReportedStep, Is.EqualTo(1));
            Assert.That(((ContactEvent)timeline.Events[2]).Contact.ReportedStep, Is.EqualTo(3));
        }
    }
}
