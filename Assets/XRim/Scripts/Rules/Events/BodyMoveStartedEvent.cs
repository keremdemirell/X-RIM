using XRim.Core;

namespace XRim.Rules.Events
{
    /// <summary>
    /// A dummy's planned body move begins, when execution starts (GDD §5, Decided). It carries the player's input, not what
    /// happened to the body, so the turn's result tells everyone, a remote client included, which side chose a backward
    /// swipe (§13: only the input triggers the wall). Playback can hook effort sounds and camera moves on it.
    /// </summary>
    public sealed class BodyMoveStartedEvent : MatchEvent
    {
        public Side Side { get; }
        public BodyMove Move { get; }

        /// <summary>A backward swipe was chosen (§13).</summary>
        public bool IsBackward => Move.IsBackward();

        public BodyMoveStartedEvent(SimTime time, Side side, BodyMove move) : base(time)
        {
            Side = side;
            Move = move;
        }
    }
}
