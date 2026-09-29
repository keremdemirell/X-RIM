using System.Collections.Generic;
using XRim.Core;
using XRim.Networking;
using XRim.Rules;
using XRim.Rules.Settings;

namespace XRim.Bots
{
    /// <summary>What a bot may know when planning: the same frozen board and public info a human sees.</summary>
    public sealed class BotContext
    {
        public Side Side { get; }
        public PlanningWindow Window { get; }
        public IReadOnlyList<WeaponId> Loadout { get; }
        public RulesSettings Settings { get; }
        public IRandom Random { get; }

        public BotContext(Side side, PlanningWindow window, IReadOnlyList<WeaponId> loadout, RulesSettings settings, IRandom random)
        {
            Side = side;
            Window = Guard.NotNull(window, nameof(window));
            Loadout = Guard.NotNull(loadout, nameof(loadout));
            Settings = Guard.NotNull(settings, nameof(settings));
            Random = Guard.NotNull(random, nameof(random));
        }
    }
}
