using NUnit.Framework;
using XRim.Bots;
using XRim.Core;
using XRim.Networking;
using XRim.Rules;
using XRim.Rules.Planning;
using XRim.Rules.Settings;
using XRim.Tests.EditMode.Networking;

namespace XRim.Tests.EditMode.Bots
{
    public sealed class BotsSanityTests
    {
        [Test]
        public void RandomBot_OnlyPicksAllowedBodyMoves()
        {
            RulesSettings settings = GddStartingValues.CreateRulesSettings();
            PlanningWindow window = NetworkingSanityTests.CreateWindow(settings);
            PlanningConstraints constraints = window.Constraints[Side.Left];
            constraints.ForbidBodyMove(BodyMove.Crouch);
            constraints.ForbidBodyMove(BodyMove.Lunge);
            constraints.ForbidBodyMove(BodyMove.StepBack);
            constraints.ForbidBodyMove(BodyMove.Jump);

            var context = new BotContext(Side.Left, window, new[] { WeaponIds.Rapier }, settings, new XorShiftRandom(TestData.Seed));
            TurnPlan plan = new RandomBotBrain().PlanTurn(context);

            Assert.That(plan.BodyMove, Is.EqualTo(BodyMove.None));
            Assert.That(plan.Weapon, Is.EqualTo(WeaponIds.Rapier));
        }

        [Test]
        public void BotPlanSource_SendsMoveThenPathThenReady()
        {
            RulesSettings settings = GddStartingValues.CreateRulesSettings();
            var authority = new FakeTurnAuthority();
            var bot = new BotPlanSource(Side.Right, new RandomBotBrain(), new[] { WeaponIds.Mace }, settings, new XorShiftRandom(TestData.Seed));

            using (new PlanSourceBinder(authority, new IPlanSource[] { bot }))
            {
                authority.RaisePlanningStarted(NetworkingSanityTests.CreateWindow(settings));
            }

            Assert.That(authority.Received.Count, Is.EqualTo(3), "the mace is already held, so no weapon switch");
            Assert.That(authority.Received[0].Command, Is.InstanceOf<SetBodyMoveCommand>());
            Assert.That(authority.Received[1].Command, Is.InstanceOf<SetPathCommand>());
            Assert.That(authority.Received[2].Command, Is.InstanceOf<SetReadyCommand>());
            Assert.That(authority.Received.TrueForAll(r => r.Side == Side.Right), Is.True);
        }
    }
}
