using System.Collections.Generic;
using NUnit.Framework;
using XRim.Core;
using XRim.Networking;
using XRim.Rules;
using XRim.Rules.Planning;
using XRim.Rules.Settings;
using XRim.Simulation;

namespace XRim.Tests.EditMode.Networking
{
    /// <summary>
    /// What each side shows the other as planning opens travels with the planning window (Session 16), so a bot reads the opponent's public
    /// weapon and Ready from the window rather than from anything hidden.
    /// </summary>
    public sealed class PlanningWindowPublicStateTests
    {
        [Test]
        public void WithoutPublicStates_TheWindowShowsTheBoardsWeapons_NobodyReady()
        {
            RulesSettings settings = GddStartingValues.CreateRulesSettings();
            PlanningWindow window = NetworkingSanityTests.CreateWindow(settings);

            foreach (Side side in new[] { Side.Left, Side.Right })
            {
                PublicPlanningState shown = window.PublicStates[side];
                Assert.That(shown.Weapon, Is.EqualTo(window.Board.State.Fighters[side].CurrentWeapon), side.ToString());
                Assert.That(shown.IsReady, Is.False, side.ToString());
                Assert.That(shown.SignatureChargedVisible, Is.False, side.ToString());
            }
        }

        [Test]
        public void TheLocalAuthority_SendsEachSidesPublicState_WithTheWindow()
        {
            RulesSettings settings = GddStartingValues.CreateRulesSettings();
            var authority = new LocalTurnAuthority(TestData.CreateMatchSetup(), settings, new SimulationSettings(), new RulePolicies(),
                new StubTurnSimulator(), new ManualClock(0.0));
            var windows = new List<PlanningWindow>();
            authority.PlanningStarted += windows.Add;

            authority.Start();

            Assert.That(windows, Has.Count.EqualTo(1));
            PlanningWindow window = windows[0];
            Assert.That(window.PublicStates.Left.Weapon, Is.EqualTo(WeaponIds.Rapier), "the left's first loadout weapon (TestData)");
            Assert.That(window.PublicStates.Right.Weapon, Is.EqualTo(WeaponIds.Mace), "the right's first loadout weapon (TestData)");
            Assert.That(window.PublicStates.Left.IsReady || window.PublicStates.Right.IsReady, Is.False);
        }
    }
}
