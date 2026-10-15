using System;
using XRim.Core;
using XRim.Rules;

namespace XRim.Simulation.Physics
{
    /// <summary>
    /// One body of the dummy as <see cref="DummyLayout"/> places it, facing +X with the pelvis at the origin, in arena units: its name (as
    /// the ragdoll builder names it), the hit zone it belongs to, the joint it turns about, its size, and its draw order.
    /// </summary>
    public sealed class DummyBodyLayout
    {
        public DummyBodyLayout(string name, BodyPart part, Vec2 pivotUnits, Vec2 sizeUnits, bool isRound, bool rises, int sortingOrder,
            bool onlyAfterDeath)
        {
            Name = Guard.NotNull(name, nameof(name));
            Part = part;
            PivotUnits = pivotUnits;
            SizeUnits = sizeUnits;
            IsRound = isRound;
            Rises = rises;
            SortingOrder = sortingOrder;
            OnlyAfterDeath = onlyAfterDeath;
        }

        public string Name { get; }

        /// <summary>The hit zone it belongs to (both segments of a ten-body arm or leg are that arm or leg).</summary>
        public BodyPart Part { get; }

        /// <summary>The point it turns about (its joint with its parent), relative to the pelvis.</summary>
        public Vec2 PivotUnits { get; }

        /// <summary>Width (x) and height or length (y); the head's diameter both ways.</summary>
        public Vec2 SizeUnits { get; }

        /// <summary>True for the head (a disc); every other body is a box.</summary>
        public bool IsRound { get; }

        /// <summary>True when it extends up from its pivot (the torso, its halves, the head); false when it hangs down (arms, legs).</summary>
        public bool Rises { get; }

        /// <summary>Its draw order: far limbs behind the torso, near limbs in front (<see cref="DummyLayout"/>).</summary>
        public int SortingOrder { get; }

        /// <summary>True for the torso's two halves: hidden until the torso splits at death (§11).</summary>
        public bool OnlyAfterDeath { get; }

        /// <summary>Its middle, in its own frame: half its height above (or below) its pivot.</summary>
        public Vec2 CentreLocalUnits => new Vec2(0f, (Rises ? 0.5f : -0.5f) * SizeUnits.Y);

        /// <summary>Its middle relative to the pelvis, standing.</summary>
        public Vec2 CentreUnits => PivotUnits + CentreLocalUnits;

        /// <summary>Its far end, in its own frame: where a hanging limb's next segment or hand is.</summary>
        public Vec2 EndLocalUnits => new Vec2(0f, (Rises ? 1f : -1f) * SizeUnits.Y);

        /// <summary>The calibration marker's diameter: a disc centred on the body, a share of its narrower side.</summary>
        public float MarkerDiameterUnits => Math.Min(SizeUnits.X, SizeUnits.Y) * DummyLayout.MarkerSizeFraction;
    }
}
