using NUnit.Framework;
using XRim.Core;
using XRim.Presentation.Feel;

namespace XRim.Tests.EditMode.Presentation
{
    /// <summary>
    /// The slow motion of a turn (Session 13, GDD pillar 1): real time everywhere except the slowed stretches, easing in and out, the slowest
    /// stretch winning where two overlap. A frame's real time becomes turn time from wherever playback stands, so a stretch starts on time
    /// whatever the frame rate.
    /// </summary>
    public sealed class SlowMotionPlanTests
    {
        private const float Tolerance = 1e-4f;

        private static SimTime At(double seconds) => SimTime.FromSeconds(seconds);

        private static SlowMotionPlan Plan(double start, double end, float speed, float rampSeconds = 0f) =>
            new SlowMotionPlan(new[] { new SlowMotionWindow(At((start + end) * 0.5), At(start), At(end), speed, rampSeconds) });

        [Test]
        public void WithoutStretches_PlaybackRunsInRealTime()
        {
            Assert.That(SlowMotionPlan.None.Advance(At(0.3), 0.016f), Is.EqualTo(0.016f));
            Assert.That(SlowMotionPlan.None.RealSecondsBetween(SimTime.Zero, At(1.0)), Is.EqualTo(1f).Within(Tolerance));
        }

        [Test]
        public void InsideAStretch_TurnTimeMovesAtItsSpeed()
        {
            SlowMotionPlan plan = Plan(0.4, 0.6, 0.25f);

            Assert.That(plan.SpeedAt(At(0.5)), Is.EqualTo(0.25f));
            Assert.That(plan.Advance(At(0.45), 0.1f), Is.EqualTo(0.025f).Within(Tolerance));
        }

        [Test]
        public void AFrameThatReachesAStretch_SlowsDownExactlyWhereItStarts()
        {
            SlowMotionPlan plan = Plan(0.5, 0.7, 0.25f);

            // 0.1 s of real time reaches the stretch at 0.5; the other 0.1 s plays at a quarter speed.
            Assert.That(plan.Advance(At(0.4), 0.2f), Is.EqualTo(0.1f + 0.025f).Within(Tolerance));
        }

        [Test]
        public void AFrameThatLeavesAStretch_IsBackToRealTimeAfterIt()
        {
            SlowMotionPlan plan = Plan(0.3, 0.5, 0.25f);

            // 0.01 s of turn at a quarter speed takes 0.04 s; the other 0.06 s plays in real time.
            Assert.That(plan.Advance(At(0.49), 0.1f), Is.EqualTo(0.01f + 0.06f).Within(Tolerance));
        }

        [Test]
        public void TheRamps_EaseFromRealTimeToTheStretchsSpeedAndBack()
        {
            SlowMotionPlan plan = Plan(0.5, 0.6, 0.2f, rampSeconds: 0.1f);

            Assert.That(plan.SpeedAt(At(0.39)), Is.EqualTo(1f));
            Assert.That(plan.SpeedAt(At(0.45)), Is.EqualTo(0.6f).Within(Tolerance), "halfway down the ramp in");
            Assert.That(plan.SpeedAt(At(0.55)), Is.EqualTo(0.2f).Within(Tolerance));
            Assert.That(plan.SpeedAt(At(0.65)), Is.EqualTo(0.6f).Within(Tolerance), "halfway up the ramp out");
            Assert.That(plan.SpeedAt(At(0.71)), Is.EqualTo(1f));
        }

        [Test]
        public void OverlappingStretches_PlayAtTheSlowerSpeed()
        {
            var plan = new SlowMotionPlan(new[]
            {
                new SlowMotionWindow(At(0.5), At(0.4), At(0.6), 0.5f, 0f),
                new SlowMotionWindow(At(0.55), At(0.5), At(0.7), 0.2f, 0f),
            });

            Assert.That(plan.SpeedAt(At(0.45)), Is.EqualTo(0.5f));
            Assert.That(plan.SpeedAt(At(0.55)), Is.EqualTo(0.2f));
            Assert.That(plan.SpeedAt(At(0.65)), Is.EqualTo(0.2f));
        }

        [Test]
        public void RealSecondsBetween_AddsTheTimeTheStretchesTake()
        {
            SlowMotionPlan plan = Plan(0.4, 0.5, 0.25f);

            // 0.9 s at real time plus 0.1 s of turn at a quarter speed (0.4 s).
            Assert.That(plan.RealSecondsBetween(SimTime.Zero, At(1.0)), Is.EqualTo(1.3f).Within(Tolerance));
        }

        [TestCase(1f / 30f)]
        [TestCase(1f / 60f)]
        [TestCase(1f / 144f)]
        public void PlayingFrameByFrame_TakesTheRealTimeThePlanSays_AtAnyFrameRate(float frameSeconds)
        {
            SlowMotionPlan plan = Plan(0.3, 0.55, 0.2f, rampSeconds: 0.04f);
            SimTime end = At(1.0);
            float planned = plan.RealSecondsBetween(SimTime.Zero, end);

            SimTime now = SimTime.Zero;
            float real = 0f;
            while (now < end)
            {
                now += SimTime.FromSeconds(plan.Advance(now, frameSeconds));
                real += frameSeconds;
            }

            Assert.That(real, Is.EqualTo(planned).Within(frameSeconds + Tolerance));
        }
    }
}
