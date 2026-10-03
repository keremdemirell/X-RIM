using System;
using XRim.Core.Gdd;

namespace XRim.Simulation.Settings
{
    /// <summary>
    /// How bodies and weapons react to a landed hit (Session 06). These shape the feel and the next turn's distance, never the
    /// damage, which the rules set (GDD §11: designer-set damage, never physics energy).
    /// </summary>
    [Serializable]
    public sealed class HitReactionSettings
    {
        /// <summary>
        /// D1 (designer, 2026-10-01): when the rules stop a kinematic blade it becomes a free body carrying this share of its
        /// path speed, so its own mass hits the body (the mace shoves harder than the rapier). 0 = a dead stop.
        /// </summary>
        [Placeholder("D1/D26: how much of its speed a stopped blade carries has no value")]
        public float ReleaseSpeedFraction = 1f;

        /// <summary>D26: a weapon stopped by its last hit is held this far back along its path, the impact recoil.</summary>
        [Placeholder("D26: the recoil distance has no value")]
        public float RecoilDistanceUnits = 30f;

        /// <summary>
        /// The struck part gets this share of the weapon's momentum (mass × path speed) as an extra kick along the weapon's
        /// motion, scaled by the speed the weapon had left (D26).
        /// </summary>
        [Placeholder("§11 the hit impulse is Tunable with no value")]
        public float PartImpulseMomentumFraction = 0.5f;

        /// <summary>
        /// E1 (designer, 2026-10-03): a landed hit pushes the victim's standing point away from the attacker and it stays
        /// there, so the next turn starts from it. False: only the impulse, and the standing spring pulls the dummy back.
        /// </summary>
        [GddTbd("§13", "Does a dummy knocked back by a hit stay where it was knocked? (not covered by the GDD)",
            Proposal = "Designer 2026-10-03: it stays knocked back")]
        public bool KnockbackPersists = true;

        /// <summary>E1: how far a hit knocks the victim per unit of the weapon's mass (the mace knocks five times as far as the rapier).</summary>
        [Placeholder("E1: the knockback distance has no value")]
        public float KnockbackUnitsPerWeaponMass = 6f;

        /// <summary>E1: the knockback plays out over this time, fast first, then easing.</summary>
        [Placeholder("E1: the knockback time has no value")]
        public float KnockbackSeconds = 0.15f;
    }
}
