using System.Collections.Generic;
using XRim.Core;
using XRim.Networking;
using XRim.Rules;
using XRim.Rules.Planning;
using XRim.Rules.Settings;

namespace XRim.Bots
{
    /// <summary>Plays one side with an <see cref="IBotBrain"/>, sending the same commands a human would.</summary>
    public sealed class BotPlanSource : IPlanSource
    {
        private readonly IBotBrain _brain;
        private readonly IReadOnlyList<WeaponId> _loadout;
        private readonly RulesSettings _settings;
        private readonly IRandom _random;

        public Side Side { get; }

        public BotPlanSource(Side side, IBotBrain brain, IReadOnlyList<WeaponId> loadout, RulesSettings settings, IRandom random)
        {
            Side = side;
            _brain = Guard.NotNull(brain, nameof(brain));
            _loadout = Guard.NotNull(loadout, nameof(loadout));
            _settings = Guard.NotNull(settings, nameof(settings));
            _random = Guard.NotNull(random, nameof(random));
        }

        public void OnPlanningStarted(PlanningWindow window, IPlanningCommandSink sink)
        {
            TurnPlan plan = _brain.PlanTurn(new BotContext(Side, window, _loadout, _settings, _random));
            if (plan.Weapon != window.Board.State.Fighters[Side].CurrentWeapon)
            {
                sink.Send(new SelectWeaponCommand(plan.Weapon));
            }

            sink.Send(new SetBodyMoveCommand(plan.BodyMove));
            if (!plan.Path.IsEmpty)
            {
                sink.Send(new SetPathCommand(plan.Path));
            }

            if (plan.PressedReady)
            {
                sink.Send(new SetReadyCommand(true));
            }
        }

        public void OnPlanningEnded()
        {
        }
    }
}
