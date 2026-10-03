using System;
using XRim.Core.Gdd;
using XRim.Rules.Combat;
using XRim.Rules.Status;

namespace XRim.Rules.Settings
{
    /// <summary>HP, limb durability, damage modifiers, the head stun, interrupts and hits per weapon (GDD §9, §11, §12).</summary>
    [Serializable]
    public sealed class DamageSettings
    {
        [Placeholder("§11 HP scale is TBD")]
        public float MaxHp = 100f;

        /// <summary>
        /// GDD §11, Tunable: one hit adds at most this fraction of a limb's durability, so severing needs 3+ hits. Must stay
        /// below 0.5, or two hits could sever.
        /// </summary>
        public float PerHitLimbCapFraction = 0.35f;

        [Placeholder("§11 limb durability is TBD; arms and legs may need different values")]
        public float ArmDurability = 40f;

        [Placeholder("§11 limb durability is TBD; arms and legs may need different values")]
        public float LegDurability = 50f;

        /// <summary>GDD §12, Tunable: damage multiplier when fighting with the off hand.</summary>
        public float OffHandDamageMultiplier = 0.8f;

        /// <summary>
        /// GDD §11 (Decided): a single head hit whose damage is above this stuns. The damage compared is the hit's, after its
        /// modifiers and before the no-instant-KO clamp (D27), so a clamped hit still stuns.
        /// </summary>
        [Placeholder("§11 head stun threshold is TBD until the HP scale is set")]
        public float HeadStunThreshold = 30f;

        /// <summary>What a stun does to the stunned player's next turn. The stagger (Session 07) shares it (§10 proposal).</summary>
        [GddTbd("§11", "Effect of a stun", Proposal = "D17 default (designer, 2026-10-03): no body move next turn")]
        public StunEffect StunEffect = StunEffect.NoBodyMove;

        /// <summary>Only with <see cref="Rules.Status.StunEffect.ShorterPlanning"/>: the stunned player's planning time is multiplied by this.</summary>
        [Placeholder("§11 'a shorter planning phase' has no value")]
        public float StunPlanningDurationFraction = 0.5f;

        /// <summary>Only with <see cref="Rules.Status.StunEffect.LessInk"/>: the stunned player's ink length is multiplied by this.</summary>
        [Placeholder("§11 'a shorter ink budget' has no value")]
        public float StunInkLengthFraction = 0.5f;

        /// <summary>
        /// Which landed hits cancel the victim's attack (§9 open point). D15 (designer, 2026-10-03): only hits on the arm that
        /// holds the weapon or on the head, so both dummies can be damaged in the same turn. The other GDD options stay
        /// switchable. A killing hit always stops the dead dummy's attack.
        /// </summary>
        public InterruptRule InterruptRule = InterruptRule.WeaponArmOrHead;

        /// <summary>Only with <see cref="Combat.InterruptRule.AboveDamageThreshold"/>: a hit interrupts when its damage is above this.</summary>
        [Placeholder("§9 'only hits above a damage threshold' has no value")]
        public float InterruptDamageThreshold = 10f;

        /// <summary>
        /// D26 (designer, 2026-10-03; not in the GDD): a weapon may land up to this many hits in one turn, each on a different
        /// body part, slowing after each (<see cref="WeaponStats.SpeedKeptAfterHitFraction"/>); its last hit stops it with a
        /// recoil. 1 = the weapon stops at its first hit.
        /// </summary>
        [Placeholder("D26: the number of hits per weapon per turn has no value")]
        public int MaxHitsPerWeaponPerTurn = 3;

        /// <summary>
        /// False: only a weapon travelling its drawn path lands hits; a weapon held still (no path, or its path is over) is an
        /// obstacle, so running into an opponent's lowered blade costs nothing.
        /// </summary>
        [GddTbd("§9", "Does a weapon that is not travelling its path deal damage? (not covered by the GDD)",
            Proposal = "Designer 2026-10-03: no, only a weapon travelling its path lands hits")]
        public bool RestingWeaponsDealDamage;
    }
}
