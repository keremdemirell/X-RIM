using System;
using System.Collections.Generic;
using XRim.Bots;
using XRim.Core;
using XRim.Networking;
using XRim.Networking.Client;
using XRim.Networking.Protocol;
using XRim.Networking.Transport;
using XRim.Rules;
using XRim.Rules.Match;
using XRim.Rules.Settings;
using XRim.Simulation;
using XRim.Simulation.Recording;

namespace XRim.Tests.EditMode.Networking.Client
{
    /// <summary>
    /// A phone with a bot at the controls: it joins through <see cref="OnlineMatchJoin"/>, picks a loadout, then plays through a
    /// <see cref="RemoteTurnAuthority"/> with the bot bound by the ordinary <see cref="PlanSourceBinder"/>, and reports each turn's playback
    /// on the next frame, as the client flow does. It keeps every event it saw, in order.
    /// </summary>
    internal sealed class OnlineBotClient
    {
        private readonly RulesSettings _rules;
        private readonly NetworkSettings _network;
        private readonly IReadOnlyList<WeaponId> _loadout;
        private readonly uint _botSeed;
        private int? _playbackToReport;

        public OnlineBotClient(Func<INetConnection> connect, RulesSettings rules, NetworkSettings network, IReadOnlyList<WeaponId> loadout,
            uint botSeed, ManualClock clock = null, Handedness handedness = Handedness.Right)
        {
            _rules = rules;
            _network = network;
            _loadout = loadout;
            _botSeed = botSeed;
            Clock = clock ?? new ManualClock();
            Connections = 0;
            Link = new ServerLink(() =>
            {
                Connections++;
                LastConnection = connect();
                return LastConnection;
            }, Clock, network);
            Join = new OnlineMatchJoin(Link, new ClientHelloMessage(handedness, new[] { SignatureMoveIds.FiveFingerExplodingTouch }));
        }

        public ManualClock Clock { get; }
        public ServerLink Link { get; }
        public OnlineMatchJoin Join { get; }
        public RemoteTurnAuthority Authority { get; private set; }

        /// <summary>Binds the bot to the authority, exactly as offline.</summary>
        public PlanSourceBinder Binder { get; private set; }

        /// <summary>How many connections the link opened (the first, then each rejoin).</summary>
        public int Connections { get; private set; }

        public INetConnection LastConnection { get; private set; }

        public List<string> Events { get; } = new List<string>();
        public List<TurnResult> Results { get; } = new List<TurnResult>();
        public List<ConnectionNoticeMessage> OpponentNotices { get; } = new List<ConnectionNoticeMessage>();
        public int Resynchronizations { get; private set; }
        public MatchOutcome Outcome { get; private set; }

        public bool IsOver => Outcome != null || Join.Stage == OnlineJoinStage.Ended;

        /// <summary>One frame of the phone: the clock moves on, the join or the match is pumped, a watched turn is reported.</summary>
        public void Frame(double seconds)
        {
            Clock.Advance(seconds);
            if (Authority == null)
            {
                Join.Tick();
                if (Join.Stage == OnlineJoinStage.Picking) Join.Pick(_loadout);
                if (Join.Stage == OnlineJoinStage.Ready) StartMatch();
                return;
            }

            if (_playbackToReport.HasValue)
            {
                Authority.NotifyPlaybackFinished(_playbackToReport.Value);
                _playbackToReport = null;
            }

            Authority.Tick();
        }

        private void StartMatch()
        {
            Authority = Join.CreateAuthority(_rules, new SimulationSettings(), new RulePolicies(), Clock, _network);
            Side side = Authority.Side;
            Authority.MatchStarted += _ => Events.Add("MatchStarted");
            Authority.PlanningStarted += window => Events.Add($"PlanningStarted:{window.TurnIndex}");
            Authority.PlanningLocked += turn => Events.Add($"PlanningLocked:{turn}");
            Authority.TurnResolved += result =>
            {
                Events.Add($"TurnResolved:{result.TurnIndex}");
                Results.Add(result);
                _playbackToReport = result.TurnIndex;
            };
            Authority.MatchEnded += outcome =>
            {
                Events.Add("MatchEnded");
                Outcome = outcome;
            };
            Authority.OpponentConnectionChanged += OpponentNotices.Add;
            Authority.Resynchronized += _ => Resynchronizations++;

            var bot = new BotPlanSource(side, new RandomBotBrain(), Authority.Setup.Fighters[side].Loadout, _rules, new XorShiftRandom(_botSeed));
            Binder = new PlanSourceBinder(Authority, new IPlanSource[] { bot });
            Authority.Start();
        }
    }
}
