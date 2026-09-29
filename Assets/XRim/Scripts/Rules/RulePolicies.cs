using XRim.Rules.Arena;
using XRim.Rules.Combat;
using XRim.Rules.Limbs;
using XRim.Rules.Paths;
using XRim.Rules.Planning;
using XRim.Rules.Signature;
using XRim.Rules.SuddenDeath;

namespace XRim.Rules
{
    /// <summary>
    /// The plug-in points for every GDD TBD the rules touch. Each property is one decision the designer has
    /// not made; swapping the implementation is how an option gets prototyped. Status effects and weapon
    /// traits are created per event, so their factories are added when they are implemented.
    /// </summary>
    public sealed class RulePolicies
    {
        public IIdleTurnPolicy IdleTurn { get; set; }
        public IPublicStatePolicy PublicState { get; set; }
        public IInkCostModel InkCost { get; set; }
        public IPathStartPolicy PathStart { get; set; }
        public IReachPolicy Reach { get; set; }
        public IStrokePolicy Stroke { get; set; }
        public IInterruptPolicy Interrupt { get; set; }
        public IRepeatContactPolicy RepeatContact { get; set; }
        public IShieldBlockModel ShieldBlock { get; set; }
        public IMobilityPenaltyPolicy MobilityPenalty { get; set; }
        public IArmlessAttackMode ArmlessAttack { get; set; }
        public ILimbRetrievalPolicy LimbRetrieval { get; set; }
        public IArenaEdgePolicy ArenaEdge { get; set; }
        public ISuddenDeathNoHitPolicy SuddenDeathNoHit { get; set; }
        public ITiePolicy SuddenDeathTie { get; set; }
        public ISignatureChargeModel SignatureCharge { get; set; }
    }
}
