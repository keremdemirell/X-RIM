using XRim.Core;
using XRim.Simulation.Recording;

namespace XRim.Presentation.Playback
{
    /// <summary>
    /// Bends how fast a recorded turn plays: hitstops and slow motion (Session 13). The turn was resolved before playback, so pacing
    /// changes only what the screen shows and when, never what happened (ARCHITECTURE §1). Because the whole turn is known before it is
    /// shown, a pacer can plan ahead: slow down just before a big moment rather than after it.
    /// </summary>
    public interface IPlaybackPacer
    {
        /// <summary>True while an effect holds playback; the turn does not finish until it lets go.</summary>
        bool IsHolding { get; }

        /// <summary>A new play of this turn starts: drop whatever the last one left and plan this one.</summary>
        void Begin(TurnResult turn);

        /// <summary>How far the timeline moves (seconds of the turn, before the player's speed) for this frame's real time.</summary>
        /// <param name="now">Where playback stands in the turn when the frame starts.</param>
        float Pace(float realDeltaSeconds, SimTime now);
    }
}
