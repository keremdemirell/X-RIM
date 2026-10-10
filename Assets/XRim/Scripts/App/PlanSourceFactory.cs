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
    /// Picks who plans each side for a match mode. The prototype's opponents are developer opponents only (D25, flagged): a second
    /// human on the same device (hot-seat), the random debug bot, or two bots to watch. Humans on this device come from
    /// <c>createHuman</c>, one per side that needs one; bots play with <see cref="RandomBotBrain"/>.
    /// </summary>
    [GddTbd("§17", "Bot opponent and practice", Proposal = "Developer opponents only: hot-seat and a random debug bot (D25)")]
    public static class PlanSourceFactory
    {
        /// <param name="createHuman">Makes the plan source of a human playing that side on this device.</param>
        /// <param name="random">Every bot's choices, seeded by the match.</param>
        /// <param name="humanSide">The side the human plays against the bot.</param>
        public static IReadOnlyList<IPlanSource> Create(MatchMode mode, MatchSetup setup, RulesSettings rules,
            Func<Side, IPlanSource> createHuman, IRandom random, Side humanSide = Side.Left)
        {
            Guard.NotNull(setup, nameof(setup));
            Guard.NotNull(rules, nameof(rules));
            Guard.NotNull(createHuman, nameof(createHuman));
            Guard.NotNull(random, nameof(random));

            switch (mode)
            {
                case MatchMode.HumanVsBot:
                    return new[] { createHuman(humanSide), CreateBot(humanSide.Opponent(), setup, rules, random) };
                case MatchMode.BotVsBot:
                    return new[] { CreateBot(Side.Left, setup, rules, random), CreateBot(Side.Right, setup, rules, random) };
                case MatchMode.HotSeat:
                    return new[] { createHuman(Side.Left), createHuman(Side.Right) };
                case MatchMode.Sandbox:
                    throw new ArgumentException("The sandbox's plan sources come from the debug tools (MatchBootstrap.StartMatch).", nameof(mode));
                default:
                    throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown match mode.");
            }
        }

        private static IPlanSource CreateBot(Side side, MatchSetup setup, RulesSettings rules, IRandom random) =>
            new BotPlanSource(side, new RandomBotBrain(), setup.Fighters[side].Loadout, rules, random);
    }
}
