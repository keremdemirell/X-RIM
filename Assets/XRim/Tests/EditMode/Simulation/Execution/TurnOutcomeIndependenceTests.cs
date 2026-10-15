using System.Linq;
using NUnit.Framework;
using XRim.Networking;
using XRim.Rules.Events;
using XRim.Rules.Match;
using XRim.Simulation.Execution;
using XRim.Simulation.Recording;

namespace XRim.Tests.EditMode.Simulation.Execution
{
    /// <summary>
    /// Feel never changes outcomes (ARCHITECTURE §1, Session 13), the engine-free half: a turn is a pure function of its inputs (the same
    /// board, plans and settings give exactly the same result, every value of it), and the code that resolves turns cannot even see
    /// the client's feel, camera, input or app layers. The Unity half plays the result through every presentation effect
    /// (<c>FeelNeverChangesTheOutcomeTests</c> in the Presentation tests).
    /// </summary>
    public sealed class TurnOutcomeIndependenceTests
    {
        private static readonly string[] ClientAssemblies = { "XRim.Config", "XRim.Presentation", "XRim.Input", "XRim.App", "XRim.DebugTools" };

        [Test]
        public void TheSameInputs_GiveExactlyTheSameTurnResult_EveryTime()
        {
            TurnResult first = ScriptedDuelTurn.Simulate();
            TurnResult second = ScriptedDuelTurn.Simulate();

            Assert.That(first.Timeline.Events.OfType<HitLandedEvent>(), Is.Not.Empty, "the scenario lands a hit");
            Assert.That(first.Timeline.Events.OfType<AttackInterruptedEvent>(), Is.Not.Empty, "and a counter-hit");
            Assert.That(first.Timeline.Events.OfType<FighterDiedEvent>(), Is.Not.Empty, "and a death");
            Assert.That(TurnFingerprint.Of(second), Is.EqualTo(TurnFingerprint.Of(first)));
        }

        [TestCase(typeof(MatchStateMachine))]
        [TestCase(typeof(TurnSimulator))]
        [TestCase(typeof(LocalTurnAuthority))]
        public void TheCodeThatResolvesTurns_CannotSeeTheClientLayers(System.Type typeInAssembly)
        {
            string[] references = typeInAssembly.Assembly.GetReferencedAssemblies().Select(name => name.Name).ToArray();

            foreach (string client in ClientAssemblies)
            {
                Assert.That(references, Has.No.Member(client), $"{typeInAssembly.Assembly.GetName().Name} references {client}");
            }
        }
    }
}
