using XRim.Core;

namespace XRim.Presentation.Moments
{
    /// <summary>
    /// A legendary moment found in a played turn (GDD pillar 1): what it was, when, who made it happen and where, so the instant replay
    /// can slow down around it with the camera on it.
    /// </summary>
    public readonly struct LegendaryMoment
    {
        public MomentKind Kind { get; }

        /// <summary>The instant of the turn it happened (the replay is centred on it).</summary>
        public SimTime Time { get; }

        /// <summary>The dummy who made it happen; null when both did (a trade, a double KO, a sudden-death tie).</summary>
        public Side? Hero { get; }

        /// <summary>Where in the arena it happened, canonical arena units (the replay's camera zooms toward it).</summary>
        public Vec2 FocusUnits { get; }

        /// <summary>A plain description for logs and debug tools, for example "Left ducked under the swing and countered".</summary>
        public string Reason { get; }

        public LegendaryMoment(MomentKind kind, SimTime time, Side? hero, Vec2 focusUnits, string reason)
        {
            Kind = kind;
            Time = time;
            Hero = hero;
            FocusUnits = focusUnits;
            Reason = reason ?? string.Empty;
        }

        public override string ToString() => $"{Kind} at {Time}: {Reason}";
    }
}
