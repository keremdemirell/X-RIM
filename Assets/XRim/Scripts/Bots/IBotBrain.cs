using XRim.Core.Gdd;
using XRim.Rules.Planning;

namespace XRim.Bots
{
    /// <summary>Decides a bot's plan for one turn. Used for offline testing now; a player-facing bot mode is TBD.</summary>
    [GddTbd("§17", "Bot opponent and practice")]
    public interface IBotBrain
    {
        TurnPlan PlanTurn(BotContext context);
    }
}
