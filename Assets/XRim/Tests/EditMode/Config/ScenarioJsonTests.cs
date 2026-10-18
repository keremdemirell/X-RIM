using System;
using NUnit.Framework;
using XRim.Config;
using XRim.Simulation.Scenarios;
using XRim.Tests.EditMode.Simulation;
using XRim.Tests.EditMode.Simulation.Scenarios;

namespace XRim.Tests.EditMode.Config
{
    /// <summary>
    /// Scenario files through Unity's JSON serializer (Session 16): the whole board, both plans, every settings value and the recorded
    /// outcome come back exactly, and text that is not a scenario is refused with a reason.
    /// </summary>
    public sealed class ScenarioJsonTests
    {
        private static readonly DateTime SavedAt = new DateTime(2026, 10, 17, 12, 30, 0);

        [Test]
        public void WriteThenRead_KeepsTheBoardPlansSettingsAndOutcome()
        {
            ScenarioOutcome outcome = ScenarioOutcome.Of(ScenarioTestData.TurnWithEveryEvent());
            Scenario scenario = ScenarioTestData.RichScenario(outcome);

            Scenario loaded = ScenarioJson.Read(ScenarioJson.Write(scenario, SavedAt));

            Assert.That(TurnFingerprint.OfBoard(loaded.Input.Board), Is.EqualTo(TurnFingerprint.OfBoard(scenario.Input.Board)));
            Assert.That(loaded.Input.Plans.Left.Path.Points, Is.EqualTo(scenario.Input.Plans.Left.Path.Points));
            Assert.That(loaded.Input.Plans.Right.Signature, Is.EqualTo(scenario.Input.Plans.Right.Signature));
            Assert.That(UnityEngine.JsonUtility.ToJson(loaded.Input.Rules), Is.EqualTo(UnityEngine.JsonUtility.ToJson(scenario.Input.Rules)));
            Assert.That(UnityEngine.JsonUtility.ToJson(loaded.Input.Simulation), Is.EqualTo(UnityEngine.JsonUtility.ToJson(scenario.Input.Simulation)));
            Assert.That(ScenarioComparison.Differences(outcome, loaded.Expected, ScenarioTolerance.Exact), Is.Empty);
        }

        [Test]
        public void Write_IsReadableText_WithTheDateAndEventSummaries()
        {
            Scenario scenario = ScenarioTestData.RichScenario(ScenarioOutcome.Of(ScenarioTestData.TurnWithEveryEvent()));

            string json = ScenarioJson.Write(scenario, SavedAt);

            Assert.That(json, Does.Contain("2026-10-17T12:30:00"));
            Assert.That(json, Does.Contain("Left hits Right's RightArm with rapier"));
            Assert.That(json, Does.Contain("\n"), "pretty-printed, so a person can read it");
        }

        [Test]
        public void Read_RefusesTextThatIsNotAScenario()
        {
            Assert.Throws<FormatException>(() => ScenarioJson.Read(string.Empty));
            Assert.Throws<FormatException>(() => ScenarioJson.Read("{}"));
        }
    }
}
