using System;
using System.Collections.Generic;
using XRim.Bots;
using XRim.Core;
using XRim.Input;
using XRim.Networking;
using XRim.Rules.Match;
using XRim.Rules.Settings;

namespace XRim.App
{
    /// <summary>Picks who plans each side for a match mode.</summary>
    public static class PlanSourceFactory
    {
        public static IReadOnlyList<IPlanSource> Create(MatchMode mode, MatchSetup setup, RulesSettings rules,
            IInputScheme inputScheme, ScreenLayout layout, IRandom random)
        {
            switch (mode)
            {
                case MatchMode.HumanVsBot:
                    return new[]
                    {
                        new TouchPlanSource(layout.OwnSide, inputScheme, layout),
                        CreateBot(layout.OwnSide.Opponent(), setup, rules, random),
                    };
                case MatchMode.BotVsBot:
                    return new[] { CreateBot(Side.Left, setup, rules, random), CreateBot(Side.Right, setup, rules, random) };
                case MatchMode.HotSeat:
                    return new HotSeatCoordinator(inputScheme, layout).CreateSources();
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
