using XRim.Core.Gdd;
using XRim.Rules.Settings;

namespace XRim.Rules.SuddenDeath
{
    /// <summary>What happens when nobody lands a hit in the sudden-death turn.</summary>
    [GddTbd("§14", "Nobody hits in sudden death", Proposal = "Repeat the turn; may need its own cap")]
    public interface ISuddenDeathNoHitPolicy
    {
        bool RepeatTurn(int turnsWithoutHit, SuddenDeathSettings settings);
    }
}
