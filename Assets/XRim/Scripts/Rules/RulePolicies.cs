using System.Collections.Generic;
using XRim.Rules.Arena;
using XRim.Rules.Combat;
using XRim.Rules.Damage;
using XRim.Rules.Limbs;
using XRim.Rules.Paths;
using XRim.Rules.Planning;
using XRim.Rules.Signature;
using XRim.Rules.Status;
using XRim.Rules.SuddenDeath;

namespace XRim.Rules
{
    /// <summary>
    /// The plug-in points for the rules. Most properties are GDD TBDs the designer has not decided; swapping the
    /// implementation is how an option gets prototyped. The path policies (start, reach, strokes) hold designer
    /// decisions (D3–D5, 2026-09-29) and stay here so each rule lives in one swappable place. A property is null
    /// until the session that implements it. Status effects are created per event, by <see cref="StatusEffects"/>; weapon
    /// traits get their factory in Session 14.
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
        /// <summary>D15 (designer, 2026-10-03): the rule <c>DamageSettings.InterruptRule</c> names; D16 swing armour per weapon (off).</summary>
        public IInterruptPolicy Interrupt { get; set; } = new SettingsInterruptPolicy();

        /// <summary>D27 (designer, 2026-10-03: the recommended default): a single hit cannot take a dummy from full HP to 0.</summary>
        public IInstantKoPolicy InstantKo { get; set; } = new NoKoFromFullHpPolicy();

        /// <summary>
        /// The "Modifiers" of Damage = BaseDamage × ZoneMultiplier × Modifiers (§11), applied in this order: off hand (§12),
        /// crush-through (§10), shield reduction (§7, A5), the body move's damage bonus (D13, 0 by default) and the
        /// follow-through of a weapon that already hit this turn (D26). Session 14 adds the weapon traits.
        /// </summary>
        public List<IDamageModifier> DamageModifiers { get; set; } = new List<IDamageModifier>
        {
            new OffHandDamageModifier(), new CrushThroughDamageModifier(), new ShieldBlockDamageModifier(),
            new BodyMoveDamageBonusModifier(), new FollowThroughDamageModifier(),
        };

        /// <summary>D17 (designer, 2026-10-03: the recommended default): the effect <c>DamageSettings.StunEffect</c> names, for the next turn.</summary>
        public IStatusEffectFactory StatusEffects { get; set; } = new SettingsStatusEffectFactory();

        /// <summary>D19 (designer, 2026-10-03: the recommended default): only the first contact between the two held items resolves.</summary>
        public IRepeatContactPolicy RepeatContact { get; set; } = new SettingsRepeatContactPolicy();

        /// <summary>
        /// D20 and D21 (designer, 2026-10-03: the recommended defaults): full block square-on to the face, partial otherwise; no
        /// special rule for heavy weapons.
        /// </summary>
        public IShieldBlockModel ShieldBlock { get; set; } = new SettingsShieldBlockModel();

        /// <summary>§12 (TBD): no penalty until Session 11 designs the leg-loss options.</summary>
        public IMobilityPenaltyPolicy MobilityPenalty { get; set; } = new NoMobilityPenaltyPolicy();

        public IArmlessAttackMode ArmlessAttack { get; set; }
        public ILimbRetrievalPolicy LimbRetrieval { get; set; }

        /// <summary>D22 (§13 stays TBD): width from <c>ArenaSettings</c>, each edge a solid invisible stop.</summary>
        public IArenaEdgePolicy ArenaEdge { get; set; } = new SolidStopArenaEdgePolicy();

        public ISuddenDeathNoHitPolicy SuddenDeathNoHit { get; set; }
        public ITiePolicy SuddenDeathTie { get; set; }
        public ISignatureChargeModel SignatureCharge { get; set; }
    }
}
