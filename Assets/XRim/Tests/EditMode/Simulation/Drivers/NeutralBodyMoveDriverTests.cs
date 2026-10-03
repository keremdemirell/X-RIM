using NUnit.Framework;
using XRim.Core;
using XRim.Rules;
using XRim.Rules.Settings;
using XRim.Simulation.Drivers;
using XRim.Simulation.Physics;

namespace XRim.Tests.EditMode.Simulation.Drivers
{
    /// <summary>No body move (GDD §5 "None", §3: an idle dummy holds its pose); a crouch is a stance (designer, 2026-10-02).</summary>
    public sealed class NeutralBodyMoveDriverTests
    {
        private const float Tolerance = 1e-3f;
        private const float StandingHeight = 180f;
        private const float CrouchedHeight = 110f;

        private static BodyMoveStart Start(float heightUnits) => new BodyMoveStart(Side.Left, new BodyPose(new Vec2(-200f, heightUnits), 0f),
            new Vec2(-180f, 3f), new Vec2(-230f, -2f), StandingHeight);

        private static NeutralBodyMoveDriver Driver(BodyMoveStats stats, BodyMoveStart start)
        {
            var driver = new NeutralBodyMoveDriver();
            driver.Begin(stats, start);
            return driver;
        }

        private static BodyMoveStats Neutral(bool standsUp) =>
            new BodyMoveStats { Move = BodyMove.None, DurationSeconds = 0.3f, StandsUpFromLowStance = standsUp };

        [Test]
        public void HoldsTheRoot_PlantsTheSolesOnTheFloor_AndIsDoneAtOnce()
        {
            BodyMoveStart start = Start(StandingHeight);
            NeutralBodyMoveDriver driver = Driver(Neutral(false), start);
            BodyMoveFrame frame = driver.Evaluate(SimTime.FromSeconds(1.0));

            Assert.That(driver.IsComplete(SimTime.Zero), Is.True);
            Assert.That(frame.Root.PositionUnits, Is.EqualTo(start.Root.PositionUnits));
            Assert.That(frame.FrontSoleUnits, Is.EqualTo(new Vec2(-180f, LegGeometry.FloorYUnits)), "stays where it stood, on the floor");
            Assert.That(frame.BackSoleUnits, Is.EqualTo(new Vec2(-230f, LegGeometry.FloorYUnits)));
        }

        [Test]
        public void ACrouchedDummy_StaysLow()
        {
            NeutralBodyMoveDriver driver = Driver(Neutral(false), Start(CrouchedHeight));

            Assert.That(driver.Evaluate(SimTime.FromSeconds(1.0)).Root.PositionUnits.Y, Is.EqualTo(CrouchedHeight));
        }

        [Test]
        public void WithoutData_HoldsThePose()
        {
            NeutralBodyMoveDriver driver = Driver(null, Start(CrouchedHeight));

            Assert.That(driver.IsComplete(SimTime.Zero), Is.True);
            Assert.That(driver.Evaluate(SimTime.FromSeconds(1.0)).Root.PositionUnits.Y, Is.EqualTo(CrouchedHeight));
        }

        [Test]
        public void StandsUpFlag_RaisesACrouchedDummyBackToStandingHeight()
        {
            BodyMoveStats neutral = Neutral(true);
            NeutralBodyMoveDriver driver = Driver(neutral, Start(CrouchedHeight));

            Assert.That(driver.IsComplete(SimTime.FromSeconds(neutral.DurationSeconds * 0.5)), Is.False);
            Assert.That(driver.IsComplete(SimTime.FromSeconds(neutral.DurationSeconds)), Is.True);
            Assert.That(driver.Evaluate(SimTime.FromSeconds(neutral.DurationSeconds)).Root.PositionUnits.Y,
                Is.EqualTo(StandingHeight).Within(Tolerance));
        }

        [Test]
        public void StandsUpFlag_LeavesAStandingDummyAlone()
        {
            NeutralBodyMoveDriver driver = Driver(Neutral(true), Start(StandingHeight));

            Assert.That(driver.IsComplete(SimTime.Zero), Is.True, "nothing to stand up from");
            Assert.That(driver.Evaluate(SimTime.FromSeconds(0.1)).Root.PositionUnits.Y, Is.EqualTo(StandingHeight));
        }
    }
}
