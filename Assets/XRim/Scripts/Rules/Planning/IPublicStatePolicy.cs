using XRim.Core.Gdd;

namespace XRim.Rules.Planning
{
    /// <summary>Builds what the opponent sees during planning, beyond the Decided weapon and Ready status.</summary>
    [GddTbd("§8", "Can the opponent see a charged signature move?", Proposal = "A chosen move stays hidden")]
    public interface IPublicStatePolicy
    {
        PublicPlanningState Build(PlanningSession session, bool signatureCharged);
    }
}
