using System;
using System.Collections.Generic;
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
    /// Whole matches between two random bots through the real authority, state machine and rules, with a scripted
    /// simulator standing in for physics: no Unity, no scene, no clock but the manual one.
    /// </summary>
    public sealed class BotMatchTests
    {
        private const int TickLimit = 500; // a safety net: no match here runs anywhere near it
        private const uint LeftSeed = 11u;
        private const uint RightSeed = 29u;

        private RulesSettings _settings;

        [SetUp]
        public void SetUp()
        {
            _settings = GddStartingValues.CreateRulesSettings();
        }

        private sealed class BotMatch : IDisposable
        {
            public LocalTurnAuthority Authority { get; }
            public ScriptedTurnSimulator Simulator { get; }
            public List<PlanningWindow> Windows { get; } = new List<PlanningWindow>();
            public List<CommandResult> BotResults { get; } = new List<CommandResult>();
            public MatchOutcome Outcome { get; private set; }
            public int EndedCount { get; private set; }

            private readonly PlanSourceBinder _binder;

            public BotMatch(RulesSettings settings, ScriptedTurnSimulator simulator, uint leftSeed, uint rightSeed)
            {
                Simulator = simulator;
                MatchSetup setup = TestData.CreateMatchSetup();
                Authority = new LocalTurnAuthority(setup, settings, new SimulationSettings(), new RulePolicies(), simulator, new ManualClock(0.0));
                Authority.PlanningStarted += Windows.Add;
                Authority.MatchEnded += outcome =>
                {
                    Outcome = outcome;
                    EndedCount++;
                };
                _binder = new PlanSourceBinder(Authority, new IPlanSource[]
                {
                    new CheckedBot(Side.Left, setup, settings, leftSeed, BotResults),
                    new CheckedBot(Side.Right, setup, settings, rightSeed, BotResults),
                });
            }

            /// <summary>Starts the match and ticks until it ends or <paramref name="stop"/> says so.</summary>
            public void Play(Func<BotMatch, bool> stop = null)
            {
                Authority.Start();
                for (int tick = 0; tick < TickLimit && Outcome == null && !(stop != null && stop(this)); tick++)
                {
                    Authority.Tick();
                }
            }

            public void Dispose() => _binder.Dispose();
        }

        /// <summary>A bot that remembers whether the authority accepted every command it sent.</summary>
        private sealed class CheckedBot : IPlanSource
        {
            private readonly BotPlanSource _inner;
            private readonly List<CommandResult> _results;

            public Side Side { get; }

            public CheckedBot(Side side, MatchSetup setup, RulesSettings settings, uint seed, List<CommandResult> results)
            {
                Side = side;
                _results = results;
                _inner = new BotPlanSource(side, new RandomBotBrain(), setup.Fighters[side].Loadout, settings, new XorShiftRandom(seed));
            }

            public void OnPlanningStarted(PlanningWindow window, IPlanningCommandSink sink) =>
                _inner.OnPlanningStarted(window, new RecordingSink(sink, _results));

            public void OnPlanningEnded() => _inner.OnPlanningEnded();
        }

        private sealed class RecordingSink : IPlanningCommandSink
        {
            private readonly IPlanningCommandSink _inner;
            private readonly List<CommandResult> _results;

            public RecordingSink(IPlanningCommandSink inner, List<CommandResult> results)
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

        // --- Matches that end ------------------------------------------------------------------

        [Test]
        public void ScriptedDamage_RunsABotMatchToAKo()
        {
            var simulator = new ScriptedTurnSimulator((turn, state) => state.Fighters[Side.Right].Hp -= 25f);
            using (var match = new BotMatch(_settings, simulator, LeftSeed, RightSeed))
            {
                match.Play();

                Assert.That(match.Outcome, Is.Not.Null, "the match ended");
                Assert.That(match.EndedCount, Is.EqualTo(1));
                Assert.That(match.Outcome.Winner, Is.EqualTo(Side.Left));
                Assert.That(match.Outcome.Reason, Is.EqualTo(MatchEndReason.KnockOut));
                Assert.That(match.Outcome.TurnCount, Is.EqualTo(4), "100 HP at 25 a turn");
                Assert.That(simulator.Inputs, Has.Count.EqualTo(4));
                Assert.That(match.Windows, Has.Count.EqualTo(4), "no planning after the KO");
                Assert.That(match.Authority.Phase, Is.EqualTo(MatchPhase.MatchOver));
            }
        }

        [Test]
        public void TheDamagedSideLosing_GivesTheOtherBotTheWin()
        {
            var simulator = new ScriptedTurnSimulator((turn, state) => state.Fighters[Side.Left].Hp -= 50f);
            using (var match = new BotMatch(_settings, simulator, LeftSeed, RightSeed))
            {
                match.Play();

                Assert.That(match.Outcome.Winner, Is.EqualTo(Side.Right));
                Assert.That(match.Outcome.TurnCount, Is.EqualTo(2));
            }
        }

        [Test]
        public void AStalledMatch_ReachesSuddenDeathSetup_AfterTurn30()
        {
            var simulator = new ScriptedTurnSimulator();
            using (var match = new BotMatch(_settings, simulator, LeftSeed, RightSeed))
            {
                match.Play(m => m.Windows.Exists(w => w.IsSuddenDeath));

                Assert.That(match.Outcome, Is.Null, "nobody won");
                Assert.That(simulator.Inputs, Has.Count.EqualTo(_settings.Match.TurnCap));
                PlanningWindow sudden = match.Windows.Find(w => w.IsSuddenDeath);
                Assert.That(sudden, Is.Not.Null);
                Assert.That(sudden.TurnIndex, Is.EqualTo(_settings.Match.TurnCap), "the 31st planning window, turn index 30");
                Assert.That(sudden.Board.State.Fighters[Side.Left].Hp, Is.EqualTo(RuleConstants.SuddenDeathHp));
                Assert.That(sudden.Board.State.Fighters[Side.Right].Hp, Is.EqualTo(RuleConstants.SuddenDeathHp));
            }
        }

        // --- The bots behave -------------------------------------------------------------------

        [Test]
        public void TheBotsNeverIdle_SoNobodyForfeits()
        {
            var simulator = new ScriptedTurnSimulator();
            using (var match = new BotMatch(_settings, simulator, LeftSeed, RightSeed))
            {
                match.Play(m => m.Windows.Exists(w => w.IsSuddenDeath));

                foreach (PlanningWindow window in match.Windows)
                {
                    if (window.IsSuddenDeath) continue;
                    Assert.That(window.Board.State.Fighters[Side.Left].ConsecutiveIdleTurns, Is.EqualTo(0), $"turn {window.TurnIndex}");
                    Assert.That(window.Board.State.Fighters[Side.Right].ConsecutiveIdleTurns, Is.EqualTo(0), $"turn {window.TurnIndex}");
                }
            }
        }

        [Test]
        public void EveryCommandTheBotsSend_IsAcceptedByTheAuthority()
        {
            var simulator = new ScriptedTurnSimulator();
            using (var match = new BotMatch(_settings, simulator, LeftSeed, RightSeed))
            {
                match.Play(m => m.Windows.Exists(w => w.IsSuddenDeath));

                Assert.That(match.BotResults, Is.Not.Empty);
                foreach (CommandResult result in match.BotResults) Assert.That(result.Accepted, Is.True, result.Rejection.ToString());
            }
        }

        [Test]
        public void EveryExecutedPlan_HasAPathInsideTheLoadoutWeaponsInk()
        {
            var simulator = new ScriptedTurnSimulator();
            MatchSetup setup = TestData.CreateMatchSetup();
            using (var match = new BotMatch(_settings, simulator, LeftSeed, RightSeed))
            {
                match.Play(m => m.Windows.Exists(w => w.IsSuddenDeath));

                foreach (TurnInput input in simulator.Inputs)
                {
                    foreach (Side side in new[] { Side.Left, Side.Right })
                    {
                        TurnPlan plan = input.Plans[side];
                        Assert.That(setup.Fighters[side].Loadout, Contains.Item(plan.Weapon));
                        Assert.That(plan.Path.IsEmpty, Is.False);
                        Assert.That(plan.Path.LengthUnits, Is.LessThanOrEqualTo(_settings.FindWeapon(plan.Weapon).InkLengthUnits + 0.5f));
                    }
                }
            }
        }

        [Test]
        public void TheSameSeeds_PlayTheSameMatch_AndOtherSeedsDoNot()
        {
            List<string> first = Fingerprint(LeftSeed, RightSeed);
            List<string> again = Fingerprint(LeftSeed, RightSeed);
            List<string> other = Fingerprint(LeftSeed + 1u, RightSeed + 1u);

            Assert.That(again, Is.EqualTo(first));
            Assert.That(other, Is.Not.EqualTo(first));
        }

        /// <summary>A short text of every plan executed in a ten-turn match.</summary>
        private List<string> Fingerprint(uint leftSeed, uint rightSeed)
        {
            var simulator = new ScriptedTurnSimulator();
            using (var match = new BotMatch(_settings, simulator, leftSeed, rightSeed))
            {
                match.Play(m => m.Simulator.Inputs.Count >= 10);
                var lines = new List<string>();
                foreach (TurnInput input in simulator.Inputs)
                {
                    foreach (Side side in new[] { Side.Left, Side.Right })
                    {
                        TurnPlan plan = input.Plans[side];
                        Vec2 end = plan.Path.Points[plan.Path.Points.Count - 1];
                        lines.Add($"{side} {plan.Weapon} {plan.BodyMove} {end.X:0.0},{end.Y:0.0}");
                    }
                }

                return lines;
            }
        }
    }
}
