using System;
using System.Collections.Generic;
using System.Diagnostics;
using XRim.Core;
using XRim.Networking;
using XRim.Rules;
using XRim.Rules.Match;
using XRim.Rules.Settings;
using XRim.Simulation;
using XRim.Simulation.Execution;
using XRim.Simulation.Recording;

namespace XRim.Bots.Balance
{
    /// <summary>
    /// One bot-vs-bot match of the balance batch, played a turn at a time (<see cref="Step"/>) through the real authority, rules and the
    /// simulator it is given, exactly as the Match scene's bot vs bot plays it minus the screen: both right-handed, a random valid loadout per
    /// side (or the default one), each bot with its own seeded random. Its <see cref="Record"/> fills as turns resolve.
    /// </summary>
    public sealed class BotMatchRun : IDisposable
    {
        /// <summary>Mixed into the match seed so the loadout picks and each bot draw from their own streams (technical).</summary>
        private const uint LoadoutSalt = 0x1F3D5B79u;
        private const uint LeftBotSalt = 0x2545F491u;
        private const uint RightBotSalt = 0x5851F42Du;

        private readonly LocalTurnAuthority _authority;
        private readonly PlanSourceBinder _binder;
        private readonly TimedSimulator _simulator;
        private readonly Func<TurnResult, double> _onScreenSeconds;
        private readonly int _turnLimit;
        private TurnResult _lastTurn;

        /// <param name="firstPlaysLeft">The matchup's first brain takes the left side (the batch alternates it).</param>
        /// <param name="onScreenSeconds">A turn's time on screen with the feel layer; null counts its recorded length.</param>
        public BotMatchRun(int seed, BalanceRun run, bool firstPlaysLeft, RulesSettings rules, SimulationSettings simulation, BotSettings bots,
            ITurnSimulator simulator, bool randomLoadouts, int turnLimit, Func<TurnResult, double> onScreenSeconds = null)
        {
            Guard.NotNull(run, nameof(run));
            Guard.NotNull(rules, nameof(rules));
            Guard.NotNull(bots, nameof(bots));
            _turnLimit = turnLimit;
            _onScreenSeconds = onScreenSeconds;
            var brains = firstPlaysLeft
                ? new PerSide<BotBrainKind>(run.Matchup.First, run.Matchup.Second)
                : new PerSide<BotBrainKind>(run.Matchup.Second, run.Matchup.First);
            PerSide<IReadOnlyList<WeaponId>> loadouts = PerSide<IReadOnlyList<WeaponId>>.Create(side => Loadout(rules, seed, side, randomLoadouts));
            Record = new BalanceMatchRecord(seed, run, brains, loadouts);

            var setup = new MatchSetup(PerSide<FighterSetup>.Create(side => new FighterSetup(Handedness.Right, loadouts[side])), (uint)seed);
            _simulator = new TimedSimulator(Guard.NotNull(simulator, nameof(simulator)));
            _authority = new LocalTurnAuthority(setup, rules, simulation, new RulePolicies(), _simulator, new ManualClock());
            _authority.TurnResolved += OnTurnResolved;
            _authority.MatchEnded += OnMatchEnded;
            var sources = new IPlanSource[]
            {
                new BotPlanSource(Side.Left, bots.CreateBrain(brains.Left), loadouts.Left, rules, new XorShiftRandom((uint)seed ^ LeftBotSalt)),
                new BotPlanSource(Side.Right, bots.CreateBrain(brains.Right), loadouts.Right, rules, new XorShiftRandom((uint)seed ^ RightBotSalt)),
            };
            _binder = new PlanSourceBinder(_authority, sources);
            _authority.Start();
        }

        public BalanceMatchRecord Record { get; }

        /// <summary>It ended, or it reached the batch's turn limit (a safety net: sudden death always ends).</summary>
        public bool IsOver => Record.Finished || Record.Turns >= _turnLimit;

        /// <summary>Plays one turn (the bots are Ready the moment planning opens, and nothing waits for a playback).</summary>
        public void Step()
        {
            if (!IsOver) _authority.Tick();
        }

        public void Dispose()
        {
            _binder.Dispose();
            _authority.TurnResolved -= OnTurnResolved;
            _authority.MatchEnded -= OnMatchEnded;
        }

        private void OnTurnResolved(TurnResult turn)
        {
            _lastTurn = turn;
            double onScreen = _onScreenSeconds != null ? _onScreenSeconds(turn) : turn.Timeline.Duration.Seconds;
            Record.Add(turn, _simulator.LastMilliseconds, onScreen);
        }

        private void OnMatchEnded(MatchOutcome outcome) => Record.Finish(outcome, _lastTurn);

        private static IReadOnlyList<WeaponId> Loadout(RulesSettings rules, int seed, Side side, bool random)
        {
            if (random && new RandomLoadoutPicker().TryPick(rules, new AllWeaponsOwned(), new XorShiftRandom(((uint)seed ^ LoadoutSalt) + (uint)side + 1u),
                    out IReadOnlyList<WeaponId> picked))
                return picked;

            var loadout = new List<WeaponId>();
            foreach (string id in rules.Loadout.DefaultLoadoutWeaponIds)
            {
                loadout.Add(new WeaponId(id));
            }

            return loadout;
        }

        /// <summary>Times each turn's simulation on this machine (the batch reports the cost; it never changes a turn).</summary>
        private sealed class TimedSimulator : ITurnSimulator
        {
            private readonly ITurnSimulator _inner;
            private readonly Stopwatch _watch = new Stopwatch();

            public TimedSimulator(ITurnSimulator inner)
            {
                _inner = inner;
            }

            public double LastMilliseconds { get; private set; }

            public TurnResult Simulate(TurnInput input)
            {
                _watch.Restart();
                TurnResult result = _inner.Simulate(input);
                _watch.Stop();
                LastMilliseconds = _watch.Elapsed.TotalMilliseconds;
                return result;
            }
        }
    }
}
