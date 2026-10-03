using System;
using XRim.Core;

namespace XRim.Rules.Combat
{
    /// <summary>
    /// What a clash did (GDD §10): the kind of interaction, which weapon came out ahead, and the numbers it was decided from
    /// (the contact angle and each weapon's power P = W_m·m + W_v·v), for the gizmos and the feel layer.
    /// </summary>
    public readonly struct ClashResult
    {
        public ClashKind Kind { get; }

        /// <summary>
        /// The weapon that came out ahead when exactly one did: the stronger in a crush-through, the lighter in a deflection.
        /// Null when both rebound or both slide past.
        /// </summary>
        public Side? Winner { get; }

        /// <summary>Stage 1: 0° = sliding along each other, 90° = square impact.</summary>
        public float ContactAngleDegrees { get; }

        public float LeftPower { get; }
        public float RightPower { get; }

        public ClashResult(ClashKind kind, Side? winner, float contactAngleDegrees, float leftPower, float rightPower)
        {
            bool needsWinner = kind == ClashKind.CrushThrough || kind == ClashKind.LighterDeflectsHeavier;
            if (needsWinner != winner.HasValue)
                throw new ArgumentException($"A {kind} clash {(needsWinner ? "needs" : "has no")} winner.", nameof(winner));

            Kind = kind;
            Winner = winner;
            ContactAngleDegrees = contactAngleDegrees;
            LeftPower = leftPower;
            RightPower = rightPower;
        }

        /// <summary>Stage 1 picked a hard clash (the angle at or above the threshold) rather than a glancing contact.</summary>
        public bool IsHardClash => Kind == ClashKind.CrushThrough || Kind == ClashKind.BothRebound;

        /// <summary>Both weapons rebound in sparks and neither continues.</summary>
        public bool Rebounds => Kind == ClashKind.BothRebound;

        public float PowerOf(Side side) => side == Side.Left ? LeftPower : RightPower;

        /// <summary>The weapon carries on along the rest of its path from the contact point.</summary>
        public bool Continues(Side side) => Kind == ClashKind.BothSlidePast || Winner == side;

        /// <summary>The weapon is knocked off its path: the loser of a crush-through, or the heavier weapon of a deflection.</summary>
        public bool IsKnockedOff(Side side) => Winner.HasValue && Winner.Value != side;

        /// <summary>The side's dummy is staggered: only the loser of a crush-through (§10). A deflected weapon just misses.</summary>
        public bool Staggers(Side side) => Kind == ClashKind.CrushThrough && IsKnockedOff(side);
    }
}
