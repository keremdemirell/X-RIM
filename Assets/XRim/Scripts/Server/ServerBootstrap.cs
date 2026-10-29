using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using UnityEngine;
using XRim.Config;
using XRim.Core;
using XRim.Networking;
using XRim.Networking.Server;
using XRim.Networking.Transport;
using XRim.Rules.Settings;
using XRim.Simulation;
using XRim.Simulation.Unity2D;

namespace XRim.Server
{
    /// <summary>
    /// Composition root of the online server (Session 19, GDD §18 server-authoritative): the only thing in the Server scene. It takes one
    /// settings snapshot from the tuning profile, builds the hidden physics world (<see cref="HeadlessPhysics"/>, the same Unity 2D physics
    /// and simulator a phone uses offline), listens for players over TCP on every network interface, and ticks the engine-free
    /// <see cref="MatchServer"/> once a frame on a clock advanced by real time. Every match on the server shares the one world: a turn is
    /// simulated inside the frame, one after another.
    /// <list type="bullet">
    /// <item>Built as a Dedicated Server player (XRim/Build/Windows Dedicated Server, Linux Dedicated Server), it runs headless; it also runs
    /// in the Editor: open the Server scene and press Play.</item>
    /// <item>The port is the Network asset's (7777), or <c>-port N</c> on the command line.</item>
    /// <item>Logs <c>[XRim Server]</c> lines: listening, every match found and started, each turn's simulation time and result size, every
    /// drop, rejoin and refusal.</item>
    /// <item>In batch mode (a dedicated server) the frame rate is capped, so an idle server does not spin a processor core.</item>
    /// </list>
    /// The tuning lives on the server: a phone receives the server's snapshot and previews with it, so changing the server's tuning
    /// changes online play for everyone after the server restarts.
    /// </summary>
    public sealed class ServerBootstrap : MonoBehaviour
    {
        private const string LogPrefix = "[XRim Server] ";

        /// <summary>Frames per second a dedicated server runs at (technical): a frame or two of delay is nothing next to the planning timer.</summary>
        private const int BatchModeFrameRate = 60;

        [Tooltip("Root of all tuning. The server plays every match with one snapshot of it, taken at start.")]
        [SerializeField] private TuningProfile _tuning;

        private readonly ManualClock _clock = new ManualClock();
        private HeadlessPhysics _physics;
        private bool _portOverridden;
        private int _requestedPort;
        private IPAddress _listenAddress = IPAddress.Any;

        /// <summary>The engine-free server; null until it started, or when it could not.</summary>
        public MatchServer Server { get; private set; }

        /// <summary>Times every simulated turn.</summary>
        public TimedTurnSimulator Simulator { get; private set; }

        /// <summary>The port it listens on (the one asked for, or the one the system picked for port 0).</summary>
        public int Port { get; private set; }

        /// <summary>Wires a server built in code (tests) before its first frame; <paramref name="port"/> 0 lets the system pick a free one.</summary>
        /// <param name="thisMachineOnly">Listen only for connections from this computer (tests: no firewall question).</param>
        public void Configure(TuningProfile tuning, int port, bool thisMachineOnly = false)
        {
            if (Server != null) throw new InvalidOperationException("Configure the server before it starts.");
            _tuning = tuning;
            _requestedPort = port;
            _portOverridden = true;
            _listenAddress = thisMachineOnly ? IPAddress.Loopback : IPAddress.Any;
        }

        private void Start()
        {
            if (_tuning == null)
            {
                Fail("No tuning profile is assigned. Run XRim/Setup/Create Default Tuning Assets, then XRim/Setup/Create Server Scene.");
                return;
            }

            var issues = new List<string>();
            RulesSettings rules = _tuning.BuildRulesSettings(issues);
            SimulationSettings simulation = _tuning.BuildSimulationSettings(issues);
            NetworkSettings network = _tuning.BuildNetworkSettings(issues);
            rules.Validate(issues);
            foreach (string issue in issues)
            {
                Debug.LogWarning(LogPrefix + issue, this);
            }

            int port = _portOverridden ? _requestedPort : ServerCommandLine.Port(Environment.GetCommandLineArgs(), network.Port);
            TcpNetListener listener;
            try
            {
                listener = new TcpNetListener(_listenAddress, port);
            }
            catch (Exception error) when (TcpNetConnection.IsSocketFault(error))
            {
                bool taken = error is System.Net.Sockets.SocketException socket && socket.SocketErrorCode == System.Net.Sockets.SocketError.AddressAlreadyInUse;
                Fail(taken
                    ? $"Port {port} is already in use: another server is running on this computer (the Server scene playing in the Editor, or " +
                      "another XRimServer window). Stop it, or start this one with -port N (the players then need that port in the Network asset)."
                    : $"Cannot listen on port {port}: {error.Message}");
                return;
            }

            _physics = new HeadlessPhysics(rules, simulation, _tuning.ArenaSpace);
            Simulator = new TimedTurnSimulator(_physics.CreateSimulator());
            Simulator.Simulated += OnTurnSimulated;
            uint seed = (uint)Environment.TickCount | 1u;
            var game = new ServerGameSetup(rules, simulation, Simulator, new SettingsTextJson().Write(rules, simulation), new XorShiftRandom(seed));
            Server = new MatchServer(listener, _clock, game, network);
            Server.Logged += OnServerLogged;
            Port = listener.Port;

            if (Application.isBatchMode)
            {
                QualitySettings.vSyncCount = 0;
                Application.targetFrameRate = BatchModeFrameRate;
            }

            Debug.Log($"{LogPrefix}Listening on port {Port} (tuning profile '{_tuning.name}', {simulation.Segmentation}, {simulation.StepRateHz} Hz). " +
                      "Waiting for players.", this);
        }

        private void Update()
        {
            if (Server == null) return;
            _clock.Advance(Time.unscaledDeltaTime);
            Server.Tick();
        }

        private void OnDestroy()
        {
            if (Server != null)
            {
                Server.Logged -= OnServerLogged;
                Server.Dispose();
                Server = null;
            }

            if (Simulator != null) Simulator.Simulated -= OnTurnSimulated;
            _physics?.Dispose();
            _physics = null;
        }

        private void OnServerLogged(string line) => Debug.Log(LogPrefix + line, this);

        private void OnTurnSimulated(int turnIndex, double milliseconds) =>
            Debug.Log(string.Format(CultureInfo.InvariantCulture, "{0}Turn {1} simulated in {2:F1} ms (mean {3:F1}, worst {4:F1} over {5} turns).",
                LogPrefix, turnIndex + 1, milliseconds, Simulator.Timing.AllTimeMean, Simulator.Timing.AllTimeMax, Simulator.Timing.Count), this);

        private void Fail(string reason)
        {
            Debug.LogError(LogPrefix + reason, this);
            enabled = false;
            if (Application.isBatchMode && !Application.isEditor) Application.Quit(1);
        }

        /// <summary>Serialized field names, for the Editor tool that builds the Server scene.</summary>
        public static class FieldNames
        {
            public const string Tuning = nameof(_tuning);
        }
    }
}
