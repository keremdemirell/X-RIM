using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using XRim.Bots;
using XRim.Core;
using XRim.Networking;
using XRim.Rules;
using XRim.Rules.Match;
using XRim.Rules.Planning;
using XRim.Rules.Settings;
using XRim.Simulation;
using XRim.Simulation.Execution;
using XRim.Tests.EditMode.Networking;

namespace XRim.Tests.EditMode.Bots
{
    /// <summary>
    /// Whole matches between bots that each picked a random loadout from the five weapons (Session 14, GDD §6), through the real
    /// pick, authority, state machine and rules, with a scripted simulator for physics. A match can be played with any of the
    /// weapons: every command the bots send is accepted, every match ends, and across a batch all five weapons are brought and used.
    /// </summary>
    public sealed class RandomLoadoutBotMatchTests
    {
        private const int Matches = 40;
        private const uint RightSeedOffset = 1000u;
        private const int TickLimit = 500;
        private const float DamagePerTurn = 25f;

        private sealed class Recording : IPlanningCommandSink
        {
            private readonly IPlanningCommandSink _inner;
            private readonly List<CommandResult> _results;

            public Recording(IPlanningCommandSink inner, List<CommandResult> results)
            {
                _inner = inner;
                _results = results;
            }

            public CommandResult Send(PlanningCommand command)
            {
                CommandResult result = _inner.Send(command);
                _results.Add(result);
                return result;
            }
        }

        private sealed class CheckedBot : IPlanSource
        {
            private readonly BotPlanSource _inner;
            private readonly List<CommandResult> _results;

            public Side Side { get; }

            public CheckedBot(Side side, IReadOnlyList<WeaponId> loadout, RulesSettings settings, uint seed, List<CommandResult> results)
            {
                Side = side;
                _results = results;
                _inner = new BotPlanSource(side, new RandomBotBrain(), loadout, settings, new XorShiftRandom(seed));
            }

            public void OnPlanningStarted(PlanningWindow window, IPlanningCommandSink sink) =>
                _inner.OnPlanningStarted(window, new Recording(sink, _results));

            public void OnPlanningEnded() => _inner.OnPlanningEnded();
        }

        private static IReadOnlyList<WeaponId> PickFor(RulesSettings settings, uint seed)
        {
            bool picked = new RandomLoadoutPicker().TryPick(settings, new AllWeaponsOwned(), new XorShiftRandom(seed), out IReadOnlyList<WeaponId> loadout);
            Assert.That(picked, Is.True);
            return loadout;
        }

        [Test]
        public void MatchesBetweenRandomLoadouts_AreAcceptedAndEnd_AndEveryWeaponIsBroughtAndUsed()
        {
            RulesSettings settings = GddStartingValues.CreateRulesSettings();
            var brought = new HashSet<WeaponId>();
            var used = new HashSet<WeaponId>();

            for (uint seed = 1; seed <= Matches; seed++)
            {
                IReadOnlyList<WeaponId> left = PickFor(settings, seed);
                IReadOnlyList<WeaponId> right = PickFor(settings, seed + RightSeedOffset);
                brought.UnionWith(left);
                brought.UnionWith(right);

                // The pick through the same rules as the screen: both loadouts valid, revealed once both are locked in.
                var selection = new LoadoutSelection(settings, new AllWeaponsOwned());
                Assert.That(selection.TryLockIn(Side.Left, left, out _), Is.True, $"seed {seed}: left");
                Assert.That(selection.TryLockIn(Side.Right, right, out _), Is.True, $"seed {seed}: right");
                var setup = new MatchSetup(selection.CreateFighters(PerSide<Handedness>.Create(_ => Handedness.Right)), seed);

                var simulator = new ScriptedTurnSimulator((turn, state) => state.Fighters[Side.Right].Hp -= DamagePerTurn);
                var results = new List<CommandResult>();
                MatchOutcome outcome = null;
                var authority = new LocalTurnAuthority(setup, settings, new SimulationSettings(), new RulePolicies(), simulator, new ManualClock(0.0));
                authority.MatchEnded += finished => outcome = finished;
                using (var binder = new PlanSourceBinder(authority, new IPlanSource[]
                {
                    new CheckedBot(Side.Left, left, settings, seed, results),
                    new CheckedBot(Side.Right, right, settings, seed + RightSeedOffset, results),
                }))
                {
                    authority.Start();
                    for (int tick = 0; tick < TickLimit && outcome == null; tick++) authority.Tick();
                }

                Assert.That(outcome, Is.Not.Null, $"seed {seed}: the match ended");
                Assert.That(outcome.Winner, Is.EqualTo(Side.Left), $"seed {seed}");
                Assert.That(results, Is.Not.Empty);
                Assert.That(results.All(result => result.Accepted), Is.True, $"seed {seed}: every command was accepted");
                foreach (TurnInput input in simulator.Inputs)
                {
                    Assert.That(left, Contains.Item(input.Plans[Side.Left].Weapon), $"seed {seed}: left used a weapon of its loadout");
                    Assert.That(right, Contains.Item(input.Plans[Side.Right].Weapon), $"seed {seed}: right used a weapon of its loadout");
                    used.Add(input.Plans[Side.Left].Weapon);
                    used.Add(input.Plans[Side.Right].Weapon);
                }
            }

            var roster = new[] { WeaponIds.Rapier, WeaponIds.Sword, WeaponIds.Mace, WeaponIds.Spear, WeaponIds.Shield };
            Assert.That(brought, Is.EquivalentTo(roster), "every weapon of the roster was picked into some loadout");
            Assert.That(used, Is.EquivalentTo(roster), "and every one of them was played");
        }
    }
}
