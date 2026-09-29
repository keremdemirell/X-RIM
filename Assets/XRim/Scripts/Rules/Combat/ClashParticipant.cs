using XRim.Core;

namespace XRim.Rules.Combat
{
    /// <summary>One weapon's side of a clash (GDD §10).</summary>
    public readonly struct ClashParticipant
    {
        public Side Side { get; }
        public float Mass { get; }
        public float SpeedUnitsPerSecond { get; }

        public ClashParticipant(Side side, float mass, float speedUnitsPerSecond)
        {
            Side = side;
            Mass = mass;
            SpeedUnitsPerSecond = speedUnitsPerSecond;
        }
    }
}
