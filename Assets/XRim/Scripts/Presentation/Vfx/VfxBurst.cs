using XRim.Core;

namespace XRim.Presentation.Vfx
{
    /// <summary>
    /// A number of particles of one kind leaving one point in a cone at once (presentation only): the recipe of an effect, before
    /// <see cref="MatchVfx"/> draws it. Arena units, degrees counter-clockwise from +X (90 = straight up).
    /// </summary>
    public readonly struct VfxBurst
    {
        public VfxParticleKind Kind { get; }
        public int Count { get; }
        public Vec2 OriginUnits { get; }

        /// <summary>The middle of the cone the particles leave in.</summary>
        public float DirectionDegrees { get; }

        /// <summary>How far either side of the direction a particle may leave.</summary>
        public float SpreadDegrees { get; }

        /// <summary>The fastest a particle leaves; each leaves at between a share of it and all of it.</summary>
        public float SpeedUnitsPerSecond { get; }

        public float SizeUnits { get; }

        /// <summary>Seconds of the turn it lasts; 0 = debris that stays on the floor until the next turn.</summary>
        public float LifetimeSeconds { get; }

        public VfxColorRole Color { get; }

        /// <summary>The side whose effect style it uses: the one who made it happen (an attacker, a blocker); null for the shared style.</summary>
        public Side? StyleSide { get; }

        public VfxBurst(VfxParticleKind kind, int count, Vec2 originUnits, float directionDegrees, float spreadDegrees, float speedUnitsPerSecond,
            float sizeUnits, float lifetimeSeconds, VfxColorRole color, Side? styleSide = null)
        {
            Kind = kind;
            Count = count;
            OriginUnits = originUnits;
            DirectionDegrees = directionDegrees;
            SpreadDegrees = spreadDegrees;
            SpeedUnitsPerSecond = speedUnitsPerSecond;
            SizeUnits = sizeUnits;
            LifetimeSeconds = lifetimeSeconds;
            Color = color;
            StyleSide = styleSide;
        }
    }
}
