using System;
using System.Collections.Generic;
using XRim.Core;
using XRim.Networking;
using XRim.Networking.Server;
using XRim.Networking.Transport;
using XRim.Rules;
using XRim.Rules.Match;
using XRim.Rules.Planning;
using XRim.Rules.Settings;
using XRim.Simulation;

namespace XRim.Tests.EditMode.Networking.Server
{
    /// <summary>
    /// A server and its players in one test, joined by a <see cref="LoopbackNetwork"/>: the real <see cref="MatchServer"/> on a manual clock,
    /// turns resolved by a <see cref="ScriptedTurnSimulator"/> that records every plan it was given.
    /// </summary>
    internal sealed class ServerHarness
    {
        /// <summary>Long enough that no test's clock jump makes a connection look silent, unless a test asks for a timeout.</summary>
        public const float NoTimeoutSeconds = 100000f;

        public const string SettingsText = "{\"settings\":\"as the server plays them\"}";

        public ServerHarness(NetworkSettings network = null, Action<int, MatchState> script = null, RulesSettings rules = null)
        {
            Rules = rules ?? GddStartingValues.CreateRulesSettings();
            Network = network ?? new NetworkSettings { ConnectionTimeoutSeconds = NoTimeoutSeconds };
            Simulator = new ScriptedTurnSimulator(script);
            var game = new ServerGameSetup(Rules, new SimulationSettings(), Simulator, SettingsText, new XorShiftRandom(TestData.Seed));
            Server = new MatchServer(Loopback.Listen(), Clock, game, Network);
            Server.Logged += Log.Add;
        }

        public LoopbackNetwork Loopback { get; } = new LoopbackNetwork();
        public ManualClock Clock { get; } = new ManualClock();
        public MatchServer Server { get; }
        public ScriptedTurnSimulator Simulator { get; }
        public RulesSettings Rules { get; }
        public NetworkSettings Network { get; }
        public List<string> Log { get; } = new List<string>();

        public TestPlayer Connect() => new TestPlayer(Loopback.Connect());

        /// <summary>A new player who says hello.</summary>
        public TestPlayer Join(Handedness handedness = Handedness.Right, IReadOnlyList<SignatureMoveId> signatures = null)
        {
            TestPlayer player = Connect();
            player.Hello(handedness, signatures);
            Tick();
            return player;
        }

        /// <summary>Two players paired, both loadouts picked: the first planning phase is open for both.</summary>
        public (TestPlayer Left, TestPlayer Right) StartMatch(IReadOnlyList<SignatureMoveId> leftSignatures = null,
            IReadOnlyList<SignatureMoveId> rightSignatures = null)
        {
            TestPlayer left = Join(Handedness.Right, leftSignatures);
            TestPlayer right = Join(Handedness.Left, rightSignatures ?? new[] { SignatureMoveIds.WhiplashCalibrationSpin });
            Read(left, right);
            left.Pick();
            right.Pick(WeaponIds.Mace, WeaponIds.Spear, WeaponIds.Shield);
            Tick();
            Read(left, right);
            return (left, right);
        }

        /// <summary>Both press Ready, the server resolves the turn, both report their playback and the next planning phase opens.</summary>
        public void PlayTurn(TestPlayer left, TestPlayer right)
        {
            left.Command(new SetReadyCommand(true));
            right.Command(new SetReadyCommand(true));
            Tick();
            Tick();
            Read(left, right);
            left.ReportPlayback();
            right.ReportPlayback();
            Tick();
            Tick();
            Read(left, right);
        }

        public void Tick() => Server.Tick();

        public void Advance(double seconds)
        {
            Clock.Advance(seconds);
            Server.Tick();
        }

        public static void Read(params TestPlayer[] players)
        {
            foreach (TestPlayer player in players)
            {
                player.Read();
            }
        }
    }
}
