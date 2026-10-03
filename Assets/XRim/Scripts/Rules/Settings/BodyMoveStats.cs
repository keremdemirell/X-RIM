using System;
using XRim.Core;
using XRim.Core.Gdd;

namespace XRim.Rules.Settings
{
    /// <summary>
    /// Data for one stance swipe (GDD §5). The concept is Decided, but the move set is TBD for feel and every distance,
    /// height and duration is Tunable, so a move is described by data, not code: where the pelvis goes, how the torso
    /// leans and where the feet stand when the move is at full extent. Every move starts when execution starts and runs
    /// alongside the weapon path (§5, Decided). The entry for <see cref="BodyMove.None"/> describes the neutral move.
    /// </summary>
    [Serializable]
    public sealed class BodyMoveStats
    {
        public BodyMove Move;

        /// <summary>
        /// Where the pelvis goes, in the fighter's frame (+X = toward the opponent), arena units. X is a step that carries
        /// over to the next turn (§3: positions carry over). Y is measured from standing height: below it the body sinks into
        /// a low stance and holds it; above it the move is a hop that is back at standing height by the end of
        /// <see cref="DurationSeconds"/>, because nothing holds a dummy in the air.
        /// </summary>
        [Placeholder("§5 distances and heights of each move are Tunable with no values")]
        public Vec2 DisplacementUnits;

        /// <summary>Time to reach full extent; a hop lands at the end of it.</summary>
        [Placeholder("§9 body move timing relative to weapon travel is Tunable")]
        public float DurationSeconds = 0.4f;

        /// <summary>The torso tilt the move ends in, degrees. Positive leans toward the opponent.</summary>
        [Placeholder("§5 the shape of each move is Tunable with no values")]
        public float LeanDegrees;

        /// <summary>Distance between the soles at full extent, the front foot ahead of the pelvis, arena units.</summary>
        [Placeholder("§5 the shape of each move is Tunable with no values")]
        public float StrideUnits;

        /// <summary>Hops only: how high the soles are tucked above the floor at the top of the hop, arena units.</summary>
        [Placeholder("§5 jump height is Tunable with no value")]
        public float FootLiftUnits;

        /// <summary>
        /// Only meaningful for <see cref="BodyMove.StepBack"/>. True: a lean that stays in place (the pelvis holds its spot
        /// and the torso tilts by <see cref="LeanInPlaceDegrees"/>). False: a real step back (D12 default).
        /// </summary>
        [GddTbd("§5", "Backward swipe: lean in place or real step", Proposal = "D12 default (2026-10-02): a real short step back")]
        public bool IsLeanInPlace;

        /// <summary>Only with <see cref="IsLeanInPlace"/>: the torso tilt that replaces the step. Negative leans away from the opponent.</summary>
        [Placeholder("§5 the lean of a lean-in-place backward move is Tunable with no value")]
        public float LeanInPlaceDegrees;

        /// <summary>Extra weapon speed along the path for the whole turn, as a fraction (0.1 = 10% faster). D13 default: 0.</summary>
        [GddTbd("§5", "Does lunge add speed or damage, or only reach?", Proposal = "D13 default (2026-10-02): reach only")]
        public float WeaponSpeedBonusFraction;

        /// <summary>Extra damage for the turn's hits, as a fraction. Applied by the damage rules (Session 06). D13 default: 0.</summary>
        [GddTbd("§5", "Does lunge add speed or damage, or only reach?", Proposal = "D13 default (2026-10-02): reach only")]
        public float DamageBonusFraction;

        /// <summary>
        /// Only meaningful for the neutral move (<see cref="BodyMove.None"/>). False: a crouched dummy stays low until another
        /// body move stands it up (a stance; §3: an idle dummy holds its pose). True: it stands back up by itself over
        /// <see cref="DurationSeconds"/>.
        /// </summary>
        [GddTbd("§5", "Does a crouch last into later turns? (not covered by the GDD)",
            Proposal = "Designer 2026-10-02: a stance that stays until another body move")]
        public bool StandsUpFromLowStance;
    }
}
