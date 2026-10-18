using System;
using NUnit.Framework;
using XRim.Core;
using XRim.Rules;
using XRim.Rules.Planning;
using XRim.Rules.Status;
using XRim.Simulation.Scenarios;

namespace XRim.Tests.EditMode.Simulation.Scenarios
{
    /// <summary>
    /// A scenario survives being turned into its save-file form and back (Session 16): the whole board, both plans, the settings, the
    /// scale and the recorded outcome come back as they were, and a file the code cannot read is refused with a reason.
    /// </summary>
    public sealed class ScenarioCodecTests
    {
        private static Scenario RoundTrip(Scenario scenario) => ScenarioCodec.FromData(ScenarioCodec.ToData(scenario));

        [Test]
        public void RoundTrip_KeepsTheWholeBoard_LimbsStatusesWallsAndPose()
        {
            Scenario scenario = ScenarioTestData.RichScenario();

            Scenario loaded = RoundTrip(scenario);

            Assert.That(TurnFingerprint.OfBoard(loaded.Input.Board), Is.EqualTo(TurnFingerprint.OfBoard(scenario.Input.Board)));
        }

        [Test]
        public void RoundTrip_GivesABoardOfItsOwn()
        {
            Scenario scenario = ScenarioTestData.RichScenario();

            Scenario loaded = RoundTrip(scenario);
            loaded.Input.Board.State.Fighters.Left.Hp = 50f;
            loaded.Input.Board.Pose.Left.Parts[(int)BodyPart.Torso].RotationDegrees = 45f;

            Assert.That(scenario.Input.Board.State.Fighters.Left.Hp, Is.EqualTo(1f));
            Assert.That(scenario.Input.Board.Pose.Left.Parts[(int)BodyPart.Torso].RotationDegrees, Is.Not.EqualTo(45f));
        }

        [Test]
        public void RoundTrip_KeepsBothPlans()
        {
            Scenario scenario = ScenarioTestData.RichScenario();

            Scenario loaded = RoundTrip(scenario);

            foreach (Side side in new[] { Side.Left, Side.Right })
            {
                TurnPlan original = scenario.Input.Plans[side];
                TurnPlan copy = loaded.Input.Plans[side];
                Assert.That(copy.Weapon, Is.EqualTo(original.Weapon), side.ToString());
                Assert.That(copy.BodyMove, Is.EqualTo(original.BodyMove), side.ToString());
                Assert.That(copy.Signature, Is.EqualTo(original.Signature), side.ToString());
                Assert.That(copy.PressedReady, Is.EqualTo(original.PressedReady), side.ToString());
                Assert.That(copy.Path.Points, Is.EqualTo(original.Path.Points), side.ToString());
            }

            Assert.That(loaded.Input.Plans.Right.Path.IsEmpty, Is.True);
            Assert.That(loaded.Input.Plans.Left.Signature.IsEmpty, Is.True);
        }

        [Test]
        public void RoundTrip_KeepsTheNameNoteScaleAndSettings()
        {
            Scenario scenario = ScenarioTestData.RichScenario();

            Scenario loaded = RoundTrip(scenario);

            Assert.That(loaded.Name, Is.EqualTo(scenario.Name));
            Assert.That(loaded.Note, Is.EqualTo(scenario.Note));
            Assert.That(loaded.WorldUnitsPerArenaUnit, Is.EqualTo(ScenarioTestData.WorldUnitsPerArenaUnit));
            Assert.That(loaded.Input.Rules, Is.SameAs(scenario.Input.Rules), "the settings are plain data, stored as they are");
            Assert.That(loaded.Input.Simulation, Is.SameAs(scenario.Input.Simulation));
        }

        [Test]
        public void RoundTrip_WithoutAnOutcome_HasNone_AndWithOne_KeepsIt()
        {
            Assert.That(RoundTrip(ScenarioTestData.RichScenario()).Expected, Is.Null);

            ScenarioOutcome outcome = ScenarioOutcome.Of(ScenarioTestData.TurnWithEveryEvent());
            Scenario loaded = RoundTrip(ScenarioTestData.RichScenario(outcome));

            Assert.That(loaded.Expected, Is.Not.Null);
            Assert.That(ScenarioComparison.Differences(outcome, loaded.Expected, ScenarioTolerance.Exact), Is.Empty);
        }

        [Test]
        public void ToData_WritesLostWeaponsInOrdinalOrder_SoTheFileIsStable()
        {
            Scenario scenario = ScenarioTestData.RichScenario();
            scenario.Input.Board.State.Fighters.Right.LostWeapons.Add(WeaponIds.Shield);
            scenario.Input.Board.State.Fighters.Right.LostWeapons.Add(WeaponIds.Mace);

            ScenarioData data = ScenarioCodec.ToData(scenario);

            Assert.That(data.State.Right.LostWeapons, Is.EqualTo(new[] { "mace", "shield" }));
        }

        [Test]
        public void ToData_RefusesAStatusEffectWithoutASavedForm()
        {
            Scenario scenario = ScenarioTestData.RichScenario();
            scenario.Input.Board.State.Fighters.Right.Statuses.Add(new HomemadeStatus());

            var refusal = Assert.Throws<NotSupportedException>(() => ScenarioCodec.ToData(scenario));

            Assert.That(refusal.Message, Does.Contain(nameof(HomemadeStatus)));
        }

        [Test]
        public void FromData_RefusesAFileFromNewerCode()
        {
            ScenarioData data = ScenarioCodec.ToData(ScenarioTestData.RichScenario());
            data.FormatVersion = ScenarioData.CurrentFormatVersion + 1;

            Assert.Throws<NotSupportedException>(() => ScenarioCodec.FromData(data));
        }

        [Test]
        public void FromData_RefusesAFileWithoutAFormatVersion()
        {
            ScenarioData data = ScenarioCodec.ToData(ScenarioTestData.RichScenario());
            data.FormatVersion = 0;

            Assert.Throws<FormatException>(() => ScenarioCodec.FromData(data));
        }

        [Test]
        public void FromData_RefusesABrokenPose()
        {
            ScenarioData data = ScenarioCodec.ToData(ScenarioTestData.RichScenario());
            data.Pose.Right.Parts = new XRim.Simulation.Physics.BodyPose[0];

            var refusal = Assert.Throws<FormatException>(() => ScenarioCodec.FromData(data));

            Assert.That(refusal.Message, Does.Contain("right"));
        }

        [Test]
        public void FromData_RefusesBrokenLimbs()
        {
            ScenarioData data = ScenarioCodec.ToData(ScenarioTestData.RichScenario());
            data.State.Left.LimbDamage = new float[2];

            Assert.Throws<FormatException>(() => ScenarioCodec.FromData(data));
        }

        [Test]
        public void FromData_RefusesAMissingScale()
        {
            ScenarioData data = ScenarioCodec.ToData(ScenarioTestData.RichScenario());
            data.WorldUnitsPerArenaUnit = 0f;

            Assert.Throws<FormatException>(() => ScenarioCodec.FromData(data));
        }

        [Test]
        public void FromData_RefusesNothingAtAll()
        {
            Assert.Throws<FormatException>(() => ScenarioCodec.FromData(null));
        }

        /// <summary>A status effect from outside the built-in three: it has no saved form.</summary>
        private sealed class HomemadeStatus : IStatusEffect
        {
            public StatusKind Kind => StatusKind.Stunned;
            public int RemainingTurns => 1;

            public void ApplyToNextTurn(PlanningConstraints constraints)
            {
            }

            public IStatusEffect WithOneTurnUsed() => null;
        }
    }
}
