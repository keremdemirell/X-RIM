using XRim.Core;

namespace XRim.Rules.Planning
{
    /// <summary>
    /// What the opponent sees during planning: the weapon and Ready (both Decided), and a charged signature move
    /// only when <c>SignatureSettings.OpponentSeesCharged</c> allows it (TBD, off by default). Body moves, paths and
    /// the chosen signature move stay hidden (GDD §3, §8).
    /// </summary>
    public sealed class DefaultPublicStatePolicy : IPublicStatePolicy
    {
        public PublicPlanningState Build(PlanningSession session, bool signatureCharged)
        {
            Guard.NotNull(session, nameof(session));
            return new PublicPlanningState(session.Weapon, session.IsReady,
                signatureCharged && session.OpponentSeesChargedSignature);
        }
    }
}
