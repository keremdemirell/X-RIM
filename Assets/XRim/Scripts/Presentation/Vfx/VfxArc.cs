using XRim.Core;

namespace XRim.Presentation.Vfx
{
    /// <summary>A crackling electric arc between two points (GDD §13 wall zap), drawn jagged and re-jagged while it lasts. Arena units.</summary>
    public readonly struct VfxArc
    {
        public Vec2 FromUnits { get; }
        public Vec2 ToUnits { get; }

        /// <summary>Seconds of the turn it lasts.</summary>
        public float LifetimeSeconds { get; }

        /// <summary>How far the jagged line may stray from the straight one, arena units.</summary>
        public float JitterUnits { get; }

        public VfxArc(Vec2 fromUnits, Vec2 toUnits, float lifetimeSeconds, float jitterUnits)
        {
            FromUnits = fromUnits;
            ToUnits = toUnits;
            LifetimeSeconds = lifetimeSeconds;
            JitterUnits = jitterUnits;
        }
    }
}
