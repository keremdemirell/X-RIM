using System.Collections.Generic;
using NUnit.Framework;
using XRim.Core;
using XRim.Networking.Server;
using XRim.Rules;
using XRim.Rules.Planning;
using XRim.Rules.Settings;
using XRim.Simulation;
using XRim.Simulation.Execution;
using XRim.Simulation.Recording;

namespace XRim.Tests.EditMode.Networking.Server
{
    /// <summary>The dedicated server's port option and its turn timer (Session 19).</summary>
    public sealed class ServerCommandLineTests
    {
        private const int Fallback = 7777;

        [TestCase(new[] { "XRimServer.exe", "-batchmode", "-port", "9000" }, 9000)]
        [TestCase(new[] { "XRimServer.exe", "-PORT", "9001" }, 9001)]
        [TestCase(new[] { "XRimServer.exe", "-port", "0" }, 0)]
        [TestCase(new[] { "XRimServer.exe" }, Fallback)]
        [TestCase(new[] { "XRimServer.exe", "-port" }, Fallback)]
        [TestCase(new[] { "XRimServer.exe", "-port", "seventy" }, Fallback)]
        [TestCase(new[] { "XRimServer.exe", "-port", "70000" }, Fallback)]
        [TestCase(new[] { "XRimServer.exe", "-port", "-5" }, Fallback)]
        [TestCase(new[] { "XRimServer.exe", "-logFile", "-port" }, Fallback)]
        public void ThePort_IsTheOneTheLineAsksFor_OrTheFallback(string[] arguments, int expected)
        {
            Assert.That(ServerCommandLine.Port(arguments, Fallback), Is.EqualTo(expected));
        }

        [Test]
        public void EveryTurn_IsTimed_AndPassedOnUnchanged()
        {
            var inner = new StubTurnSimulator();
            var timed = new TimedTurnSimulator(inner);
            var seen = new List<int>();
            timed.Simulated += (turn, milliseconds) => seen.Add(turn);
            RulesSettings rules = GddStartingValues.CreateRulesSettings();
            var input = new TurnInput(TestData.CreateInitialBoard(rules),
                new PerSide<TurnPlan>(TurnPlan.Empty(WeaponIds.Rapier), TurnPlan.Empty(WeaponIds.Mace)), rules, new SimulationSettings());

            TurnResult result = timed.Simulate(input);

            Assert.That(inner.CallCount, Is.EqualTo(1));
            Assert.That(result.TurnIndex, Is.EqualTo(0));
            Assert.That(seen, Is.EqualTo(new[] { 0 }));
            Assert.That(timed.Timing.Count, Is.EqualTo(1));
        }
    }
}
