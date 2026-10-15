using System.Linq;
using NUnit.Framework;
using XRim.Core;
using XRim.Rules;
using XRim.Rules.Arena;
using XRim.Rules.Events;
using XRim.Rules.Match;
using XRim.Rules.Paths;
using XRim.Rules.Planning;
using XRim.Rules.Settings;
using XRim.Simulation;
using XRim.Simulation.Execution;
using XRim.Simulation.Physics;
using XRim.Simulation.Recording;

namespace XRim.Tests.EditMode.Simulation.Execution
{
    /// <summary>
    /// A drawn path is a fact of the turn's result from its first instant (Session 13, A3): which side swung and with what, for the
    /// swing's sound and grunt and the moment detector. A side without a path swings nothing; the rules never read it.
    /// </summary>
    public sealed class AttackStartedEventTests
    {
        private static readonly WeaponPath Thrust = new WeaponPath(new[] { new Vec2(400f, 100f), new Vec2(640f, 100f) });

        [Test]
        public void ASideWithADrawnPath_StartsAnAttackAtTheFirstInstant_WithItsWeapon()
        {
            TurnResult result = Simulate(Plan(WeaponIds.Mace, Thrust), Plan(WeaponIds.Rapier, WeaponPath.Empty));

            AttackStartedEvent attack = result.Timeline.Events.OfType<AttackStartedEvent>().Single();
            Assert.That(attack.Side, Is.EqualTo(Side.Left));
            Assert.That(attack.Weapon, Is.EqualTo(WeaponIds.Mace));
            Assert.That(attack.Kind, Is.EqualTo(WeaponKind.Weapon));
            Assert.That(attack.Time, Is.EqualTo(SimTime.Zero));
        }

        [Test]
        public void AShieldRaised_IsAnAttackOfTheShieldKind()
        {
            TurnResult result = Simulate(Plan(WeaponIds.Rapier, WeaponPath.Empty), Plan(WeaponIds.Shield, Thrust));

            Assert.That(result.Timeline.Events.OfType<AttackStartedEvent>().Single().Kind, Is.EqualTo(WeaponKind.Shield));
        }

        [Test]
        public void NoPaths_NoAttacks()
        {
            TurnResult result = Simulate(Plan(WeaponIds.Rapier, WeaponPath.Empty), Plan(WeaponIds.Rapier, WeaponPath.Empty));

            Assert.That(result.Timeline.Events.OfType<AttackStartedEvent>(), Is.Empty);
        }

        private static TurnPlan Plan(WeaponId weapon, WeaponPath path) => new TurnPlan(weapon, BodyMove.None, path, default, true);

        private static TurnResult Simulate(TurnPlan left, TurnPlan right)
        {
            RulesSettings rules = GddStartingValues.CreateRulesSettings();
            var state = new MatchState(
                PerSide<FighterState>.Create(side => new FighterState(Handedness.Right, rules.Damage.MaxHp, (side == Side.Left ? left : right).Weapon)),
                PerSide<ElectricWallState>.Create(_ => new ElectricWallState()));
            var pose = new PoseSnapshot();
            pose.Left.Set(BodyPart.Torso, new BodyPose(Vec2.Zero, 0f));
            pose.Right.Set(BodyPart.Torso, new BodyPose(new Vec2(600f, 0f), 0f));
            pose.Left.HeldItem = new BodyPose(new Vec2(0f, 100f), 0f);
            pose.Right.HeldItem = new BodyPose(new Vec2(600f, 100f), 180f);
            using (var world = new FakePhysicsWorld())
            {
                return new TurnSimulator(world, new RulePolicies()).Simulate(new TurnInput(new BoardSnapshot(state, pose),
                    new PerSide<TurnPlan>(left, right), rules, new SimulationSettings()));
            }
        }
    }
}
