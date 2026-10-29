using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using XRim.Bots;
using XRim.Config;
using XRim.Core;
using XRim.Networking;
using XRim.Networking.Client;
using XRim.Networking.Protocol;
using XRim.Networking.Transport;
using XRim.Rules;
using XRim.Rules.Match;
using XRim.Rules.Settings;
using XRim.Server;
using XRim.Simulation;
using XRim.Simulation.Recording;
using XRim.Tests.PlayMode.App;

namespace XRim.Tests.PlayMode.Server
{
    /// <summary>
    /// The online server for real (Session 19): <see cref="ServerBootstrap"/> with the project's tuning and real Unity 2D physics, and two
    /// phones with heuristic bots at the controls, each joining over a real TCP connection, reading the server's settings from its text,
    /// and playing through <see cref="RemoteTurnAuthority"/>, until the match ends. It logs what a turn really costs on this machine: the
    /// simulation time and the size of each result as sent.
    /// </summary>
    public sealed class ServerPlayModeTests
    {
        /// <summary>A heuristic match lasts about 12–14 turns and at most 30 plus 4 of sudden death; this many seconds means it is stuck.</summary>
        private const float MatchTimeoutSeconds = 120f;

        private static readonly Regex ResultBytes = new Regex(@"(\d+) bytes sent", RegexOptions.CultureInvariant);

        [UnityTest]
        public IEnumerator TwoBotPhones_PlayAWholeMatch_ThroughTheRealServer_OverTcp()
        {
            TuningProfile tuning = ProjectTuning.Load();
            if (tuning == null) Assert.Ignore("No tuning profile: run XRim/Setup/Create Default Tuning Assets first.");

            var host = new GameObject("XRimServer (test)");
            var server = host.AddComponent<ServerBootstrap>();
            server.Configure(tuning, 0, true);
            yield return null; // Start: the server listens
            Assert.That(server.Server, Is.Not.Null, "the server started (see the Console)");
            var log = new List<string>();
            server.Server.Logged += log.Add;

            NetworkSettings network = tuning.BuildNetworkSettings(new List<string>());
            var left = new BotPhone(server.Port, network, new[] { WeaponIds.Rapier, WeaponIds.Mace, WeaponIds.Shield }, 11u);
            var right = new BotPhone(server.Port, network, new[] { WeaponIds.Mace, WeaponIds.Spear, WeaponIds.Shield }, 22u);
            float until = Time.realtimeSinceStartup + MatchTimeoutSeconds;
            try
            {
                while (!(left.IsOver && right.IsOver) && Time.realtimeSinceStartup < until)
                {
                    left.Frame(Time.unscaledDeltaTime);
                    right.Frame(Time.unscaledDeltaTime);
                    yield return null;
                }

                Assert.That(left.Outcome, Is.Not.Null, $"the left phone saw the match end; it is at {left.Stage}");
                Assert.That(right.Outcome, Is.Not.Null, $"the right phone saw the match end; it is at {right.Stage}");
                Assert.That(right.Outcome.Winner, Is.EqualTo(left.Outcome.Winner));
                Assert.That(right.Outcome.Reason, Is.EqualTo(left.Outcome.Reason));
                Assert.That(left.Results.Count, Is.EqualTo(left.Outcome.TurnCount), "every turn's result reached the phone");

                List<int> sizes = log.Select(line => ResultBytes.Match(line)).Where(match => match.Success)
                    .Select(match => int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture)).ToList();
                RollingStatistics timing = server.Simulator.Timing;
                Debug.Log(string.Format(CultureInfo.InvariantCulture,
                    "[XRim Server test] {0} turns, {1} wins by {2}. Results as sent: mean {3:N0} bytes, largest {4:N0}. Simulation on this machine: " +
                    "mean {5:F1} ms, worst {6:F1} ms. Longest timeline: {7} frames.",
                    left.Outcome.TurnCount, left.Outcome.Winner, left.Outcome.Reason, sizes.Average(), sizes.Max(), timing.AllTimeMean,
                    timing.AllTimeMax, left.Results.Max(result => result.Timeline.Frames.Count)));
            }
            finally
            {
                left.Leave();
                right.Leave();
                Object.Destroy(host);
            }

            yield return null;
        }

        /// <summary>A phone with a heuristic bot at the controls: the same client path a player's phone takes, over TCP.</summary>
        private sealed class BotPhone
        {
            private readonly ManualClock _clock = new ManualClock();
            private readonly NetworkSettings _network;
            private readonly IReadOnlyList<WeaponId> _loadout;
            private readonly uint _seed;
            private readonly OnlineMatchJoin _join;
            private RemoteTurnAuthority _authority;
            private PlanSourceBinder _binder;
            private int? _toReport;

            public BotPhone(int port, NetworkSettings network, IReadOnlyList<WeaponId> loadout, uint seed)
            {
                _network = network;
                _loadout = loadout;
                _seed = seed;
                var link = new ServerLink(() => TcpNetConnection.Connect("127.0.0.1", port), _clock, network);
                _join = new OnlineMatchJoin(link, new ClientHelloMessage(Handedness.Right, new[] { SignatureMoveIds.FiveFingerExplodingTouch }));
            }

            public List<TurnResult> Results { get; } = new List<TurnResult>();
            public MatchOutcome Outcome { get; private set; }
            public string Stage => _authority != null ? "the match" : _join.Stage.ToString();
            public bool IsOver => Outcome != null || _join.Stage == OnlineJoinStage.Ended;

            public void Frame(float seconds)
            {
                _clock.Advance(seconds);
                if (_authority == null)
                {
                    _join.Tick();
                    if (_join.Stage == OnlineJoinStage.Picking) _join.Pick(_loadout);
                    if (_join.Stage == OnlineJoinStage.Ready) Start();
                    return;
                }

                if (_toReport.HasValue)
                {
                    _authority.NotifyPlaybackFinished(_toReport.Value);
                    _toReport = null;
                }

                _authority.Tick();
            }

            public void Leave()
            {
                _binder?.Dispose();
                if (_authority != null) _authority.Leave();
                else _join.Link.Close();
            }

            private void Start()
            {
                new SettingsTextJson().Read(_join.SettingsText, out RulesSettings rules, out SimulationSettings simulation);
                _authority = _join.CreateAuthority(rules, simulation, new RulePolicies(), _clock, _network);
                _authority.TurnResolved += result =>
                {
                    Results.Add(result);
                    _toReport = result.TurnIndex;
                };
                _authority.MatchEnded += outcome => Outcome = outcome;
                Side side = _authority.Side;
                var bot = new BotPlanSource(side, new HeuristicBotBrain(), _authority.Setup.Fighters[side].Loadout, rules, new XorShiftRandom(_seed));
                _binder = new PlanSourceBinder(_authority, new IPlanSource[] { bot });
                _authority.Start();
            }
        }
    }
}
