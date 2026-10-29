using System.Collections.Generic;
using XRim.Core;
using XRim.Networking.Protocol;
using XRim.Rules;
using XRim.Rules.Settings;
using XRim.Simulation;

namespace XRim.Networking.Client
{
    /// <summary>
    /// The phone's way into an online match (Session 19), before there is anything to plan: it opens the link and says hello, waits for the
    /// server to find an opponent (which brings this player's side and the server's settings), sends the blind loadout pick (GDD §6), and
    /// waits until both loadouts are revealed. Then <see cref="CreateAuthority"/> hands the link to a <see cref="RemoteTurnAuthority"/>
    /// with everything that arrived meanwhile, so the match flow above it starts as it does offline. Call <see cref="Tick"/> every frame.
    /// </summary>
    public sealed class OnlineMatchJoin
    {
        private readonly List<NetMessage> _received = new List<NetMessage>();
        private readonly List<NetMessage> _afterStart = new List<NetMessage>();
        private IReadOnlyList<WeaponId> _pick;
        private bool _handedOver;

        public OnlineMatchJoin(ServerLink link, ClientHelloMessage hello)
        {
            Link = Guard.NotNull(link, nameof(link));
            Link.Open(Guard.NotNull(hello, nameof(hello)));
        }

        public ServerLink Link { get; }

        public OnlineJoinStage Stage { get; private set; } = OnlineJoinStage.FindingOpponent;

        /// <summary>The side the server gave this player (known from <see cref="OnlineJoinStage.Picking"/> on).</summary>
        public Side Side { get; private set; }

        /// <summary>The server's settings as text (<see cref="ISettingsTextCodec"/>), known from <see cref="OnlineJoinStage.Picking"/> on.</summary>
        public string SettingsText { get; private set; } = string.Empty;

        /// <summary>Both loadouts and the starting board, known at <see cref="OnlineJoinStage.Ready"/>.</summary>
        public MatchStartedMessage Started { get; private set; }

        /// <summary>The opponent's last connection notice before the match started (they may leave while picking).</summary>
        public ConnectionNoticeMessage OpponentNotice { get; private set; }

        public void Tick()
        {
            if (_handedOver || Stage == OnlineJoinStage.Ended) return;
            _received.Clear();
            Link.Tick(_received);
            foreach (NetMessage message in _received)
            {
                if (Stage == OnlineJoinStage.Ready)
                {
                    _afterStart.Add(message);
                    continue;
                }

                switch (message)
                {
                    case MatchFoundMessage found:
                        Side = found.YourSide;
                        SettingsText = found.SettingsText;
                        if (Stage == OnlineJoinStage.WaitingForPicks) Link.Send(new LoadoutPickMessage(_pick)); // rejoined: the pick again (ignored if held)
                        else Stage = OnlineJoinStage.Picking;
                        break;
                    case MatchStartedMessage started:
                        Started = started;
                        Stage = OnlineJoinStage.Ready;
                        break;
                    case ConnectionNoticeMessage notice:
                        OpponentNotice = notice;
                        break;
                }
            }

            if (Link.IsEnded && Stage != OnlineJoinStage.Ready) Stage = OnlineJoinStage.Ended;
        }

        /// <summary>Sends the player's blind loadout pick (the first weapon is the one the match starts with).</summary>
        public void Pick(IReadOnlyList<WeaponId> loadout)
        {
            Guard.NotNull(loadout, nameof(loadout));
            if (Stage != OnlineJoinStage.Picking) throw new System.InvalidOperationException($"A loadout is picked when an opponent is found, not at {Stage}.");
            _pick = new List<WeaponId>(loadout);
            Link.Send(new LoadoutPickMessage(_pick));
            Stage = OnlineJoinStage.WaitingForPicks;
        }

        /// <summary>
        /// The authority the match is played through, with the server's settings (read from <see cref="SettingsText"/>) and this match's
        /// rule policies. The join hands its link over and does nothing more.
        /// </summary>
        public RemoteTurnAuthority CreateAuthority(RulesSettings rules, SimulationSettings simulation, RulePolicies policies, IClock clock,
            NetworkSettings network = null)
        {
            if (Stage != OnlineJoinStage.Ready || _handedOver) throw new System.InvalidOperationException($"The match cannot start at {Stage}.");
            _handedOver = true;
            return new RemoteTurnAuthority(Link, Side, Started, _afterStart, rules, simulation, policies, clock, network);
        }
    }
}
