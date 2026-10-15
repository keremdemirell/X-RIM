using XRim.Core;

namespace XRim.Simulation.Physics
{
    /// <summary>
    /// A hinge of the dummy as <see cref="DummyLayout"/> places it: the two bodies it joins, where it is (relative to the pelvis, facing +X,
    /// arena units), and how far the child may turn against the parent (degrees, counter-clockwise positive).
    /// </summary>
    public sealed class DummyJointLayout
    {
        public DummyJointLayout(string name, string childBody, string parentBody, Vec2 anchorUnits, float minDegrees, float maxDegrees)
        {
            Name = Guard.NotNull(name, nameof(name));
            ChildBody = Guard.NotNull(childBody, nameof(childBody));
            ParentBody = Guard.NotNull(parentBody, nameof(parentBody));
            AnchorUnits = anchorUnits;
            MinDegrees = minDegrees;
            MaxDegrees = maxDegrees;
        }

        /// <summary>For example "Neck", "RightShoulder", "LeftKnee".</summary>
        public string Name { get; }

        public string ChildBody { get; }
        public string ParentBody { get; }

        /// <summary>The hinge point, which is also the child body's pivot.</summary>
        public Vec2 AnchorUnits { get; }

        public float MinDegrees { get; }
        public float MaxDegrees { get; }
    }
}
