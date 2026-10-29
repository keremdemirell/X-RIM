using System;
using System.Collections.Generic;
using XRim.Bots;
using XRim.Core;
using XRim.Networking;
using XRim.Networking.Server;
using XRim.Networking.Transport;
using XRim.Rules;
using XRim.Rules.Match;
using XRim.Rules.Settings;
using XRim.Simulation;

namespace XRim.Tests.EditMode.Networking.Client
{
    /// <summary>
    /// A whole online match in one test: the real <see cref="MatchServer"/> (turns from a <see cref="ScriptedTurnSimulator"/>) and two
    /// <see cref="OnlineBotClient"/>s, each with its own clock, joined by any transport; <see cref="Run"/> plays frames until both have seen the end.
    /// </summary>
    internal sealed class OnlineMatchRun
    {
        public const double FrameSeconds = 1.0 / 60.0;
        public const uint LeftBotSeed = 11u;
        public const uint RightBotSeed = 22u;

        public static readonly WeaponId[] LeftLoadout = { WeaponIds.Rapier, WeaponIds.Mace, WeaponIds.Shield };
        public static readonly WeaponId[] RightLoadout = { WeaponIds.Mace, WeaponIds.Spear, WeaponIds.Shield };

        /// <param name="knockOutTurn">The turn (0-based) after which the right dummy has no HP left.</param>
        /// <param name="connect">Opens a client connection; null uses the loopback network the server listens on.</param>
        public OnlineMatchRun(int knockOutTurn, INetListener listener = null, Func<INetConnection> connect = null)
        {
            Rules = GddStartingValues.CreateRulesSettings();
            Network = new NetworkSettings();
            Simulator = new ScriptedTurnSimulator(KnockOutAfter(knockOutTurn));
            var game = new ServerGameSetup(Rules, new SimulationSettings(), Simulator, "settings", new XorShiftRandom(TestData.Seed));
            Server = new MatchServer(listener ?? Loopback.Listen(), ServerClock, game, Network);
            connect = connect ?? (() => Loopback.Connect());
            Left = new OnlineBotClient(connect, Rules, Network, LeftLoadout, LeftBotSeed, new ManualClock(500.0));
            ServerTick();
            Right = new OnlineBotClient(connect, Rules, Network, RightLoadout, RightBotSeed, new ManualClock(-37.25));
        }

        public LoopbackNetwork Loopback { get; } = new LoopbackNetwork();
        public ManualClock ServerClock { get; } = new ManualClock();
        public MatchServer Server { get; }
        public ScriptedTurnSimulator Simulator { get; }
        public RulesSettings Rules { get; }
        public NetworkSettings Network { get; }
        public OnlineBotClient Left { get; }
        public OnlineBotClient Right { get; }

        /// <summary>Frames until both phones have seen the match end (or the limit); returns how many ran.</summary>
        public int Run(int maxFrames = 20000, Action<int> everyFrame = null)
        {
            int frame = 0;
            for (; frame < maxFrames && !(Left.IsOver && Right.IsOver); frame++)
            {
                everyFrame?.Invoke(frame);
                ServerClock.Advance(FrameSeconds);
                ServerTick();
                Left.Frame(FrameSeconds);
                Right.Frame(FrameSeconds);
            }

            return frame;
        }

        public void ServerTick() => Server.Tick();

        public static Action<int, MatchState> KnockOutAfter(int turn) => (index, state) =>
        {
            if (index == turn) state.Fighters.Right.Hp = 0f;
        };

        /// <summary>The same two bots with the same seeds, offline: a <see cref="LocalTurnAuthority"/> on this device. Returns its simulator and events.</summary>
        public static (ScriptedTurnSimulator Simulator, List<string> Events) PlayOffline(int knockOutTurn)
        {
            RulesSettings rules = GddStartingValues.CreateRulesSettings();
            var signatures = new[] { SignatureMoveIds.FiveFingerExplodingTouch };
            var setup = new MatchSetup(new PerSide<FighterSetup>(
                new FighterSetup(Handedness.Right, LeftLoadout, signatures), new FighterSetup(Handedness.Right, RightLoadout, signatures)), TestData.Seed);
            var simulator = new ScriptedTurnSimulator(KnockOutAfter(knockOutTurn));
            var authority = new LocalTurnAuthority(setup, rules, new SimulationSettings(), new RulePolicies(), simulator, new ManualClock());
            var events = new List<string>();
            bool over = false;
            authority.MatchStarted += _ => events.Add("MatchStarted");
            authority.PlanningStarted += window => events.Add($"PlanningStarted:{window.TurnIndex}");
            authority.PlanningLocked += turn => events.Add($"PlanningLocked:{turn}");
            authority.TurnResolved += result => events.Add($"TurnResolved:{result.TurnIndex}");
            authority.MatchEnded += _ =>
            {
                events.Add("MatchEnded");
                over = true;
            };

            var bots = new IPlanSource[]
            {
                new BotPlanSource(Side.Left, new RandomBotBrain(), LeftLoadout, rules, new XorShiftRandom(LeftBotSeed)),
                new BotPlanSource(Side.Right, new RandomBotBrain(), RightLoadout, rules, new XorShiftRandom(RightBotSeed)),
            };
            using (new PlanSourceBinder(authority, bots))
            {
                authority.Start();
                for (int tick = 0; tick < 1000 && !over; tick++)
                {
                    authority.Tick();
                }
            }

            return (simulator, events);
        }
    }
}
