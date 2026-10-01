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
    /// The plug-in points for the rules. Most properties are GDD TBDs the designer has not decided; swapping the
    /// implementation is how an option gets prototyped. The path policies (start, reach, strokes) hold designer
    /// decisions (D3–D5, 2026-09-29) and stay here so each rule lives in one swappable place. A property is null
    /// until the session that implements it. Status effects and weapon traits are created per event, so their
    /// factories are added when they are implemented.
    /// </summary>
    public sealed class RulePolicies
    {
        /// <summary>D10 (not decided): any body move, drawn path or signature move counts as a move for the forfeit counter.</summary>
        public IIdleTurnPolicy IdleTurn { get; set; } = new AnyMoveIdleTurnPolicy();

        /// <summary>The opponent sees the weapon and Ready; a charged signature move only if allowed (§8, TBD).</summary>
        public IPublicStatePolicy PublicState { get; set; } = new DefaultPublicStatePolicy();
        /// <summary>GDD §6: plain length, plus the bend penalty for weapons whose rigidity is enabled (keep or cut: TBD).</summary>
        public IInkCostModel InkCost { get; set; } = new RigidityInkCostModel();

        /// <summary>D3 (Decided): start anywhere, with a lead-in from the weapon tip that costs ink and time.</summary>
        public IPathStartPolicy PathStart { get; set; } = new LeadInFromTipPathStartPolicy();

        /// <summary>D4 (Decided): clamp the path to the reach limit.</summary>
        public IReachPolicy Reach { get; set; } = new ClampToReachPolicy();

        /// <summary>D5 (Decided): one continuous stroke per turn; redrawing replaces it.</summary>
        public IStrokePolicy Stroke { get; set; } = new ReplaceStrokePolicy();
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
