using System;
using NUnit.Framework;
using UnityEngine;
using XRim.Config;
using XRim.Rules.Settings;
using XRim.Simulation;

namespace XRim.Tests.EditMode.Config
{
    /// <summary>
    /// The server's settings travel to the phones as JSON text (Session 19): every rules and simulation value comes back as it was, and text
    /// that holds no settings is refused.
    /// </summary>
    public sealed class SettingsTextJsonTests
    {
        [Test]
        public void TheSettings_ComeBackAsTheyWere()
        {
            RulesSettings rules = GddStartingValues.CreateRulesSettings();
            rules.Match.PlanningDurationSeconds = 9.5f;
            var simulation = new SimulationSettings { StepRateHz = 300 };
            var codec = new SettingsTextJson();

            codec.Read(codec.Write(rules, simulation), out RulesSettings readRules, out SimulationSettings readSimulation);

            Assert.That(JsonUtility.ToJson(readRules), Is.EqualTo(JsonUtility.ToJson(rules)));
            Assert.That(JsonUtility.ToJson(readSimulation), Is.EqualTo(JsonUtility.ToJson(simulation)));
            Assert.That(readRules.Weapons.Count, Is.EqualTo(rules.Weapons.Count));
        }

        [TestCase("")]
        [TestCase("not json at all")]
        [TestCase("{}")]
        [TestCase("{\"Rules\":{}}")]
        public void TextWithoutSettings_IsRefused(string text)
        {
            Assert.Throws<FormatException>(() => new SettingsTextJson().Read(text, out _, out _));
        }
    }
}
