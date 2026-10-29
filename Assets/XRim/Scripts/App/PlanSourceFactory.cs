using System;
using System.Collections.Generic;
using XRim.Bots;
using XRim.Core;
using XRim.Core.Gdd;
using XRim.Networking;
using XRim.Rules.Match;
using XRim.Rules.Settings;

namespace XRim.App
{
    /// <summary>
    /// Picks who plans each side for a match mode. The opponents are practice and development opponents only (D25; GDD §17, designer
    /// 2026-10-17: no game mode): a second human on the same device (hot-seat), a bot, or two bots to watch. Humans on this device come from
    /// <c>createHuman</c>, one per side that needs one; each bot plays with the brain <see cref="BotSettings"/> names for it (Session 16:
    /// the heuristic bot by default, the random bot selectable).
    /// </summary>
    [GddTbd("§17", "Bot opponent and practice", Proposal = "Practice and development only (designer 2026-10-17): hot-seat and bots")]
    public static class PlanSourceFactory
    {
        /// <param name="createHuman">Makes the plan source of a human playing that side on this device.</param>
        /// <param name="random">Every bot's choices, seeded by the match.</param>
        /// <param name="humanSide">The side the human plays against the bot, or online (the side the server gave them).</param>
        /// <param name="bots">Which brain plays each bot side; null: the code defaults (the heuristic bot everywhere).</param>
        public static IReadOnlyList<IPlanSource> Create(MatchMode mode, MatchSetup setup, RulesSettings rules,
            Func<Side, IPlanSource> createHuman, IRandom random, Side humanSide = Side.Left, BotSettings bots = null)
        {
            Guard.NotNull(setup, nameof(setup));
            Guard.NotNull(rules, nameof(rules));
            Guard.NotNull(createHuman, nameof(createHuman));
            Guard.NotNull(random, nameof(random));
            bots = bots ?? new BotSettings();

            switch (mode)
            {
                case MatchMode.HumanVsBot:
                    return new[] { createHuman(humanSide), CreateBot(humanSide.Opponent(), bots.CreateBrain(bots.VersusBrain), setup, rules, random) };
                case MatchMode.BotVsBot:
                    return new[]
                    {
                        CreateBot(Side.Left, bots.CreateBrain(bots.WatchLeftBrain), setup, rules, random),
                        CreateBot(Side.Right, bots.CreateBrain(bots.WatchRightBrain), setup, rules, random),
                    };
                case MatchMode.HotSeat:
                    return new[] { createHuman(Side.Left), createHuman(Side.Right) };
                case MatchMode.Online:
                    return new[] { createHuman(humanSide) }; // the other side plans on its own device, through the server
                case MatchMode.Sandbox:
                    throw new ArgumentException("The sandbox's plan sources come from the debug tools (MatchBootstrap.StartMatch).", nameof(mode));
                default:
                    throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown match mode.");
            }
        }

        private static IPlanSource CreateBot(Side side, IBotBrain brain, MatchSetup setup, RulesSettings rules, IRandom random) =>
            new BotPlanSource(side, brain, setup.Fighters[side].Loadout, rules, random);
    }
}
