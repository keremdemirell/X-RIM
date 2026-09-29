using System;
using NUnit.Framework;
using XRim.Core;
using XRim.Simulation.Execution;
using XRim.Simulation.Physics;

namespace XRim.Tests.EditMode.Simulation.Execution
{
    /// <summary>
    /// GDD §9: time-to-impact to the millisecond. The blade is swept between recorded poses to find when it first
    /// touched the contact point inside a step. Rapier-like blade: 400 long, 10 wide (half-width 5).
    /// </summary>
    public sealed class ImpactTimeRefinerTests
    {
        private const long StepMicroseconds = 4_000L;
        private const long ToleranceMicroseconds = 2L;
        private static readonly BladeShape Rapier = new BladeShape(400f, 10f);

        private static PoseSample Sample(long microseconds, float gripX, float rotationDegrees = 0f) =>
            new PoseSample(new SimTime(microseconds), new BodyPose(new Vec2(gripX, 0f), rotationDegrees));

        private static void AssertTime(SimTime actual, long expectedMicroseconds) =>
            Assert.That(actual.Microseconds, Is.EqualTo(expectedMicroseconds).Within(ToleranceMicroseconds));

        [Test]
        public void Thrust_FindsWhenTheTipArrives()
        {
            // The tip reaches x = 415 (5 short of the point, the half-width) when the grip is at 15 of 40.
            SimTime time = ImpactTimeRefiner.Refine(new Vec2(420f, 0f), Rapier, 0f,
                new[] { Sample(0L, 0f), Sample(StepMicroseconds, 40f) });

            AssertTime(time, 1_500L);
        }

        [Test]
        public void Slash_FindsWhenTheEdgeArrives()
        {
            // The blade turns 0° → 90° about the grip; a point 200 out at 45° is touched at 45° − asin(5 / 200).
            var point = Vec2.FromAngleDegrees(45f) * 200f;
            double touchDegrees = 45.0 - Math.Asin(5.0 / 200.0) * 180.0 / Math.PI;

            SimTime time = ImpactTimeRefiner.Refine(point, Rapier, 0f,
                new[] { new PoseSample(SimTime.Zero, new BodyPose(Vec2.Zero, 0f)), new PoseSample(new SimTime(StepMicroseconds), new BodyPose(Vec2.Zero, 90f)) });

            AssertTime(time, (long)Math.Round(StepMicroseconds * touchDegrees / 90.0));
        }

        [Test]
        public void TwoStepWindow_FindsTheStepTheTouchHappenedIn()
        {
            PoseSample[] window = { Sample(0L, 0f), Sample(StepMicroseconds, 40f), Sample(2 * StepMicroseconds, 80f) };

            AssertTime(ImpactTimeRefiner.Refine(new Vec2(430f, 0f), Rapier, 0f, window), 2_500L);
            AssertTime(ImpactTimeRefiner.Refine(new Vec2(470f, 0f), Rapier, 0f, window), 6_500L);
        }

        [Test]
        public void TouchDistance_CountsAsTouching()
        {
            SimTime time = ImpactTimeRefiner.Refine(new Vec2(420f, 0f), Rapier, 5f,
                new[] { Sample(0L, 0f), Sample(StepMicroseconds, 40f) });

            AssertTime(time, 1_000L);
        }

        [Test]
        public void AlreadyTouching_IsTheWindowStart()
        {
            SimTime time = ImpactTimeRefiner.Refine(new Vec2(200f, 3f), Rapier, 0f,
                new[] { Sample(1_000L, 0f), Sample(5_000L, 40f) });

            Assert.That(time.Microseconds, Is.EqualTo(1_000L));
        }

        [Test]
        public void NeverTouching_IsTheClosestApproach()
        {
            // The tip passes x = 500 at the end of the first step; after that the point stays 20 away.
            PoseSample[] window = { Sample(0L, 0f), Sample(StepMicroseconds, 100f), Sample(2 * StepMicroseconds, 200f) };

            AssertTime(ImpactTimeRefiner.Refine(new Vec2(500f, 20f), Rapier, 0f, window), StepMicroseconds);
        }
    }
}
