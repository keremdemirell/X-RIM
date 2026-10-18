using System.Collections.Generic;
using NUnit.Framework;
using XRim.Bots;

namespace XRim.Tests.EditMode.Bots
{
    /// <summary>The Bots tuning asset's settings (Session 16): which brain each kind makes, and values that cannot work.</summary>
    public sealed class BotSettingsTests
    {
        [Test]
        public void ByDefault_TheHeuristicBotPlaysEverywhere()
        {
            var settings = new BotSettings();

            Assert.That(settings.VersusBrain, Is.EqualTo(BotBrainKind.Heuristic));
            Assert.That(settings.WatchLeftBrain, Is.EqualTo(BotBrainKind.Heuristic));
            Assert.That(settings.WatchRightBrain, Is.EqualTo(BotBrainKind.Heuristic));
        }

        [Test]
        public void EachKind_MakesItsBrain()
        {
            var settings = new BotSettings();

            Assert.That(settings.CreateBrain(BotBrainKind.Heuristic), Is.InstanceOf<HeuristicBotBrain>());
            Assert.That(settings.CreateBrain(BotBrainKind.Random), Is.InstanceOf<RandomBotBrain>());
        }

        [Test]
        public void TheStartingValues_AreValid()
        {
            var issues = new List<string>();
            new BotSettings().Validate(issues);

            Assert.That(issues, Is.Empty);
        }

        [Test]
        public void AChanceOutsideZeroToOne_OrANegativeAimError_IsReported()
        {
            var settings = new BotSettings();
            settings.Heuristic.DuckChance = 1.5f;
            settings.Heuristic.AimErrorUnits = -1f;
            var issues = new List<string>();

            settings.Validate(issues);

            Assert.That(issues, Has.Count.EqualTo(2));
            Assert.That(issues, Has.Some.Contains("DuckChance"));
        }
    }
}
