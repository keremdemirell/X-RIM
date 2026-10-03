using System;
using XRim.Core.Gdd;

namespace XRim.Simulation.Settings
{
    /// <summary>
    /// How weapons and dummies react when the clash and block rules stop a weapon (Session 07, designer 2026-10-03, A6). These
    /// shape the feel and the next turn's distance, never who wins: the rules decide that (GDD §7, §10).
    /// </summary>
    [Serializable]
    public sealed class ClashReactionSettings
    {
        /// <summary>A rebounding weapon (a hard clash between close powers, or a full shield block) bounces back at this share of its speed.</summary>
        [Placeholder("§10 'rebound in a shower of sparks' has no speed")]
        public float ReboundSpeedFraction = 0.5f;

        /// <summary>
        /// A weapon knocked off its path is kicked away from the winner along the contact normal at the winner's momentum
        /// (its mass × the speed the rules move it) divided by the loser's mass, times this share.
        /// </summary>
        [Placeholder("§10 'knocked off its path' has no strength")]
        public float KnockOffMomentumFraction = 0.5f;

        /// <summary>After a knock-off the weapon flies free this long, then the hand holds it wherever it ended up: visibly off its path.</summary>
        [Placeholder("§10 how long a knocked-off weapon flies free has no value")]
        public float KnockOffFreeSeconds = 0.15f;

        /// <summary>
        /// A full shield block pushes the holder back with this share of what a hit by the same weapon would (the knock-back
        /// distance and the impulse): D21's "physics knockback does the rest", since the shield itself follows its path.
        /// </summary>
        [Placeholder("D21: how hard a block pushes the holder has no value")]
        public float BlockKnockbackFraction = 0.5f;
    }
}
