using System;
using XRim.Core;
using XRim.Core.Gdd;

namespace XRim.Rules.Settings
{
    /// <summary>
    /// Data for one stance swipe (GDD §5). Distances, heights and timing are Tunable; the open
    /// questions are exposed as fields so each option can be prototyped.
    /// </summary>
    [Serializable]
    public sealed class BodyMoveStats
    {
        public BodyMove Move;

        /// <summary>Root displacement in the fighter's frame (+X = toward the opponent), arena units.</summary>
        [Placeholder("§5 distances and heights of each move are Tunable with no values")]
        public Vec2 DisplacementUnits;

        [Placeholder("§9 body move timing relative to weapon travel is Tunable")]
        public float DurationSeconds = 0.4f;

        /// <summary>Only meaningful for <see cref="BodyMove.StepBack"/>.</summary>
        [GddTbd("§5", "Backward swipe: lean in place or real step")]
        public bool IsLeanInPlace;

        [GddTbd("§5", "Does lunge add speed or damage, or only reach?")]
        public float WeaponSpeedBonusFraction;

        [GddTbd("§5", "Does lunge add speed or damage, or only reach?")]
        public float DamageBonusFraction;
    }
}
