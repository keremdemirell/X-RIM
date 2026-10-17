using XRim.Core;

namespace XRim.Rules.Events
{
    /// <summary>
    /// A dummy performs a signature move this turn (GDD §8), from the first instant of execution: the chosen move stayed hidden through
    /// planning and is revealed here, inside the turn's result. Playback shouts the absurd name on it. Never read by the rules: the move's
    /// path and special property already took effect.
    /// </summary>
    public sealed class SignatureUsedEvent : MatchEvent
    {
        public Side Side { get; }
        public SignatureMoveId Move { get; }

        /// <summary>The move's name as the catalogue had it ("Head Impact Simulation #404"); the id when it has none.</summary>
        public string DisplayName { get; }

        public SignatureUsedEvent(SimTime time, Side side, SignatureMoveId move, string displayName) : base(time)
        {
            Side = side;
            Move = move;
            DisplayName = string.IsNullOrEmpty(displayName) ? move.Value : displayName;
        }
    }
}
