using System;
using XRim.Core.Gdd;

namespace XRim.Rules.Settings
{
    /// <summary>
    /// Electric wall anti-stalemate (GDD §13). Trigger and sequence are Decided; numbers are Tunable. The geometry is the designer's
    /// (P1, 2026-10-11; not in the GDD): the wall stands on the floor behind its owner, away from the opponent; distances are along the
    /// floor from the dummy's pelvis.
    /// </summary>
    [Serializable]
    public sealed class ElectricWallSettings
    {
        /// <summary>HP a zap takes off: flat, no zone multiplier and no limb damage (P2).</summary>
        [Placeholder("§13 wall damage is Tunable with no value")]
        public float Damage = 10f;

        /// <summary>How far a zap throws the dummy forward, toward the opponent; it stays there, like a knockback (P1).</summary>
        [Placeholder("§13 bounce force is Tunable with no value; a distance the zap throws the dummy forward")]
        public float BounceDistanceUnits = 80f;

        /// <summary>How far the wall moves toward the opponent at each zap (§13: "the wall moves further forward").</summary>
        [Placeholder("§13 advance distance is Tunable with no value")]
        public float AdvanceDistanceUnits = 50f;

        /// <summary>How far behind the dummy's pelvis the warning wall springs up when its step back ends (P1).</summary>
        [Placeholder("§13 'appears behind the dummy': distance not given")]
        public float SpawnOffsetBehindUnits = 60f;

        /// <summary>Added to <see cref="Damage"/> for each zap in a row after the first (0: every zap costs the same).</summary>
        [GddTbd("§13", "Does wall damage grow with each retreat?", Proposal = "Flat (0); designer 2026-10-11: the recommended default")]
        public float DamageGrowthPerConsecutiveRetreat = 0f;

        /// <summary>
        /// A dummy touching its active wall again in the same turn, after the retreat's own zap (a hit knocked it back into it), takes the
        /// wall's damage again (no bounce, no advance). The warning wall never hurts.
        /// </summary>
        [GddTbd("§13", "Wall damage when knocked into it", Proposal = "No; designer 2026-10-11: the recommended default")]
        public bool DamageWhenKnockedInto = false;

        /// <summary>Off: no wall in sudden death (both go down when it starts and backward swipes raise none). On: they carry on.</summary>
        [GddTbd("§14", "Electric wall in sudden death",
            Proposal = "Off: at 1 HP a zap would be a cheap death; designer 2026-10-11: the recommended default")]
        public bool ActiveInSuddenDeath = false;
    }
}
