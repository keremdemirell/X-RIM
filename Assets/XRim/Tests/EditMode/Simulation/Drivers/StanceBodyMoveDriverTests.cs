using NUnit.Framework;
using XRim.Core;
using XRim.Rules;
using XRim.Rules.Settings;
using XRim.Simulation.Drivers;
using XRim.Simulation.Physics;

namespace XRim.Tests.EditMode.Simulation.Drivers
{
    /// <summary>
    /// GDD §5: crouch, lunge, step back and jump played from their data. The left dummy's pelvis stands at x = −350, the right
    /// one's at +350, both at the standing height 180 with their soles on the floor under the pelvis.
    /// </summary>
    public sealed class StanceBodyMoveDriverTests
    {
        private const float Tolerance = 1e-3f;
        private const float StandingHeight = 180f;
        private const float LeftX = -350f;
        private const float RightX = 350f;

        private RulesSettings _rules;

        [SetUp]
        public void SetUp() => _rules = GddStartingValues.CreateRulesSettings();

        private BodyMoveStats Stats(BodyMove move) => _rules.FindBodyMove(move);

        private static BodyMoveStart Start(Side side, float heightUnits = StandingHeight)
        {
            float x = side == Side.Left ? LeftX : RightX;
            return new BodyMoveStart(side, new BodyPose(new Vec2(x, heightUnits), 0f), new Vec2(x, 0f), new Vec2(x, 0f), StandingHeight);
        }

        private static StanceBodyMoveDriver Driver(BodyMoveStats stats, BodyMoveStart start)
        {
            var driver = new StanceBodyMoveDriver();
            driver.Begin(stats, start);
            return driver;
        }

        private static SimTime At(double seconds) => SimTime.FromSeconds(seconds);

        private static void AssertNear(Vec2 actual, Vec2 expected, string what)
        {
            Assert.That(actual.X, Is.EqualTo(expected.X).Within(Tolerance), what + " x");
            Assert.That(actual.Y, Is.EqualTo(expected.Y).Within(Tolerance), what + " y");
        }

        [TestCase(BodyMove.Crouch)]
        [TestCase(BodyMove.Lunge)]
        [TestCase(BodyMove.StepBack)]
        [TestCase(BodyMove.Jump)]
        public void EveryMove_StartsExactlyWhereTheDummyStands(BodyMove move)
        {
            foreach (Side side in new[] { Side.Left, Side.Right })
            {
                BodyMoveStart start = Start(side);
                BodyMoveFrame frame = Driver(Stats(move), start).Evaluate(SimTime.Zero);

                AssertNear(frame.Root.PositionUnits, start.Root.PositionUnits, $"{side} root");
                Assert.That(frame.Root.RotationDegrees, Is.EqualTo(0f).Within(Tolerance), $"{side} upright");
                AssertNear(frame.FrontSoleUnits, start.FrontSoleUnits, $"{side} front sole");
                AssertNear(frame.BackSoleUnits, start.BackSoleUnits, $"{side} back sole");
            }
        }

        [Test]
        public void Crouch_SinksToItsDepthWithinItsDuration_AndHoldsIt()
        {
            BodyMoveStats crouch = Stats(BodyMove.Crouch);
            StanceBodyMoveDriver driver = Driver(crouch, Start(Side.Left));
            float depth = StandingHeight + crouch.DisplacementUnits.Y;

            Assert.That(depth, Is.LessThan(StandingHeight), "a crouch goes down");
            Assert.That(driver.IsComplete(At(crouch.DurationSeconds * 0.9)), Is.False);
            Assert.That(driver.IsComplete(At(crouch.DurationSeconds)), Is.True);
            AssertNear(driver.Evaluate(At(crouch.DurationSeconds)).Root.PositionUnits, new Vec2(LeftX, depth), "at the end of its duration");
            AssertNear(driver.Evaluate(At(1.5)).Root.PositionUnits, new Vec2(LeftX, depth), "held to the end of the turn");
        }

        [Test]
        public void Crouch_FromACrouch_StaysAtTheSameDepth()
        {
            BodyMoveStats crouch = Stats(BodyMove.Crouch);
            float depth = StandingHeight + crouch.DisplacementUnits.Y;
            StanceBodyMoveDriver driver = Driver(crouch, Start(Side.Left, depth));

            Assert.That(driver.Evaluate(At(crouch.DurationSeconds * 0.5)).Root.PositionUnits.Y, Is.EqualTo(depth).Within(Tolerance),
                "heights are measured from standing height, not from where the turn starts");
        }

        [Test]
        public void Lunge_StepsTowardTheOpponent_OnBothSides()
        {
            BodyMoveStats lunge = Stats(BodyMove.Lunge);
            double end = lunge.DurationSeconds;

            float leftX = Driver(lunge, Start(Side.Left)).Evaluate(At(end)).Root.PositionUnits.X;
            float rightX = Driver(lunge, Start(Side.Right)).Evaluate(At(end)).Root.PositionUnits.X;

            Assert.That(leftX, Is.EqualTo(LeftX + lunge.DisplacementUnits.X).Within(Tolerance));
            Assert.That(rightX, Is.EqualTo(RightX - lunge.DisplacementUnits.X).Within(Tolerance));
        }

        [Test]
        public void StepBack_StepsAwayFromTheOpponent_AndCarriesOver()
        {
            BodyMoveStats step = Stats(BodyMove.StepBack);
            StanceBodyMoveDriver driver = Driver(step, Start(Side.Right));

            Assert.That(step.IsLeanInPlace, Is.False, "D12 default: a real step");
            Assert.That(driver.Evaluate(At(1.5)).Root.PositionUnits.X, Is.EqualTo(RightX - step.DisplacementUnits.X).Within(Tolerance),
                "the right dummy steps toward +X, away from the opponent");
        }

        [Test]
        public void Lean_TipsTheTorsoTowardTheOpponent_OnBothSides()
        {
            BodyMoveStats lunge = Stats(BodyMove.Lunge);
            Assert.That(lunge.LeanDegrees, Is.GreaterThan(0f), "the lunge leans in");
            var aboveThePelvis = new Vec2(0f, 120f);

            foreach (Side side in new[] { Side.Left, Side.Right })
            {
                BodyPose root = Driver(lunge, Start(side)).Evaluate(At(lunge.DurationSeconds)).Root;
                float headShift = aboveThePelvis.Rotated(root.RotationDegrees).X * side.FacingSign();

                Assert.That(root.RotationDegrees, Is.EqualTo(-side.FacingSign() * lunge.LeanDegrees).Within(Tolerance),
                    $"{side}: leaning in turns the left dummy clockwise, the right one counter-clockwise");
                Assert.That(headShift, Is.GreaterThan(0f), $"{side}: the head moves toward the opponent");
            }
        }

        [Test]
        public void StepBack_LeanInPlace_KeepsThePelvis_AndTiltsAway()
        {
            BodyMoveStats step = Stats(BodyMove.StepBack);
            step.IsLeanInPlace = true;

            BodyPose root = Driver(step, Start(Side.Left)).Evaluate(At(step.DurationSeconds)).Root;

            AssertNear(root.PositionUnits, new Vec2(LeftX, StandingHeight), "the pelvis stays");
            Assert.That(root.RotationDegrees, Is.EqualTo(-step.LeanInPlaceDegrees).Within(Tolerance), "turned counter-clockwise");
            Assert.That(new Vec2(0f, 120f).Rotated(root.RotationDegrees).X, Is.LessThan(0f), "the upper body sways away from the opponent");
        }

        [Test]
        public void HoldMoves_EaseInAndOut()
        {
            BodyMoveStats lunge = Stats(BodyMove.Lunge);
            StanceBodyMoveDriver driver = Driver(lunge, Start(Side.Left));
            double duration = lunge.DurationSeconds;
            float Travelled(double seconds) => driver.Evaluate(At(seconds)).Root.PositionUnits.X - LeftX;

            Assert.That(Travelled(duration * 0.5), Is.EqualTo(lunge.DisplacementUnits.X * 0.5f).Within(Tolerance), "halfway at half time");
            Assert.That(Travelled(duration * 0.1), Is.LessThan(lunge.DisplacementUnits.X * 0.1f), "starts gently");
            Assert.That(Travelled(duration * 0.9), Is.GreaterThan(lunge.DisplacementUnits.X * 0.9f), "arrives gently");
        }

        [Test]
        public void Stride_PutsTheFrontSoleAheadOfThePelvis_OnTheFloor()
        {
            BodyMoveStats lunge = Stats(BodyMove.Lunge);
            foreach (Side side in new[] { Side.Left, Side.Right })
            {
                BodyMoveFrame frame = Driver(lunge, Start(side)).Evaluate(At(lunge.DurationSeconds));
                float pelvis = frame.Root.PositionUnits.X;
                float facing = side.FacingSign();

                Assert.That((frame.FrontSoleUnits.X - pelvis) * facing, Is.EqualTo(lunge.StrideUnits * 0.5f).Within(Tolerance), $"{side} front");
                Assert.That((frame.BackSoleUnits.X - pelvis) * facing, Is.EqualTo(-lunge.StrideUnits * 0.5f).Within(Tolerance), $"{side} back");
                Assert.That(frame.FrontSoleUnits.Y, Is.EqualTo(LegGeometry.FloorYUnits), $"{side} on the floor");
                Assert.That(frame.BackSoleUnits.Y, Is.EqualTo(LegGeometry.FloorYUnits), $"{side} on the floor");
            }
        }

        [Test]
        public void Jump_PeaksHalfway_TucksTheFeet_AndLandsStanding()
        {
            BodyMoveStats jump = Stats(BodyMove.Jump);
            StanceBodyMoveDriver driver = Driver(jump, Start(Side.Left));
            double duration = jump.DurationSeconds;
            BodyMoveFrame top = driver.Evaluate(At(duration * 0.5));
            BodyMoveFrame landed = driver.Evaluate(At(duration));

            Assert.That(top.Root.PositionUnits.Y, Is.EqualTo(StandingHeight + jump.DisplacementUnits.Y).Within(Tolerance), "peak");
            Assert.That(top.FrontSoleUnits.Y, Is.EqualTo(jump.FootLiftUnits).Within(Tolerance), "soles tucked up at the top");
            Assert.That(landed.Root.PositionUnits.Y, Is.EqualTo(StandingHeight).Within(Tolerance), "back on its feet");
            Assert.That(landed.FrontSoleUnits.Y, Is.EqualTo(LegGeometry.FloorYUnits).Within(Tolerance), "soles on the floor");
            Assert.That(driver.IsComplete(At(duration * 0.9)), Is.False, "still in the air");
            Assert.That(driver.IsComplete(At(duration)), Is.True);
        }

        [Test]
        public void Jump_RisesLikeAThrownBody_FastAtTakeOff_SlowAtTheTop()
        {
            BodyMoveStats jump = Stats(BodyMove.Jump);
            StanceBodyMoveDriver driver = Driver(jump, Start(Side.Left));
            float Height(double fraction) => driver.Evaluate(At(jump.DurationSeconds * fraction)).Root.PositionUnits.Y - StandingHeight;

            Assert.That(Height(0.25), Is.EqualTo(jump.DisplacementUnits.Y * 0.75f).Within(Tolerance), "a parabola: 3/4 of the height at 1/4 of the time");
            Assert.That(Height(0.75), Is.EqualTo(Height(0.25)).Within(Tolerance), "symmetric");
        }

        [Test]
        public void Jump_FromACrouch_SpringsUp_AndLandsStanding()
        {
            BodyMoveStats jump = Stats(BodyMove.Jump);
            float crouched = StandingHeight + Stats(BodyMove.Crouch).DisplacementUnits.Y;
            StanceBodyMoveDriver driver = Driver(jump, Start(Side.Left, crouched));

            Assert.That(driver.Evaluate(SimTime.Zero).Root.PositionUnits.Y, Is.EqualTo(crouched).Within(Tolerance));
            Assert.That(driver.Evaluate(At(jump.DurationSeconds * 0.5)).Root.PositionUnits.Y,
                Is.EqualTo(StandingHeight + jump.DisplacementUnits.Y).Within(Tolerance));
            Assert.That(driver.Evaluate(At(jump.DurationSeconds)).Root.PositionUnits.Y, Is.EqualTo(StandingHeight).Within(Tolerance));
        }

        [Test]
        public void ZeroDuration_IsAtFullExtentAtOnce()
        {
            BodyMoveStats crouch = Stats(BodyMove.Crouch);
            crouch.DurationSeconds = 0f;
            StanceBodyMoveDriver driver = Driver(crouch, Start(Side.Left));

            Assert.That(driver.IsComplete(SimTime.Zero), Is.True);
            Assert.That(driver.Evaluate(SimTime.Zero).Root.PositionUnits.Y, Is.EqualTo(StandingHeight + crouch.DisplacementUnits.Y).Within(Tolerance));
        }

        [Test]
        public void Factory_PlaysNoMoveAndMissingDataAsNeutral_AndEverySwipeFromData()
        {
            var factory = new StanceBodyMoveDriverFactory();

            Assert.That(factory.Create(BodyMove.None, _rules, null), Is.InstanceOf<NeutralBodyMoveDriver>());
            Assert.That(factory.Create(BodyMove.Crouch, _rules, null), Is.InstanceOf<StanceBodyMoveDriver>());
            Assert.That(factory.Create(BodyMove.Jump, _rules, null), Is.InstanceOf<StanceBodyMoveDriver>());
            _rules.BodyMoves.Remove(Stats(BodyMove.Lunge));
            Assert.That(factory.Create(BodyMove.Lunge, _rules, null), Is.InstanceOf<NeutralBodyMoveDriver>(), "no data: hold the pose");
        }

        [Test]
        public void StartingValues_HaveTheNeutralMoveAndEverySwipe()
        {
            foreach (BodyMove move in new[] { BodyMove.None, BodyMove.Crouch, BodyMove.Lunge, BodyMove.StepBack, BodyMove.Jump })
            {
                Assert.That(Stats(move), Is.Not.Null, move.ToString());
            }

            Assert.That(Stats(BodyMove.Lunge).WeaponSpeedBonusFraction, Is.Zero, "D13 default: reach only");
            Assert.That(Stats(BodyMove.Lunge).DamageBonusFraction, Is.Zero, "D13 default: reach only");
            Assert.That(Stats(BodyMove.None).StandsUpFromLowStance, Is.False, "a crouch is a stance (designer, 2026-10-02)");
        }
    }
}
