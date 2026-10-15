using XRim.Core;
using XRim.Input;
using XRim.Presentation.Moments;

namespace XRim.App
{
    /// <summary>
    /// While an instant replay plays, every touch is the tap that skips it: none may draw a path or swipe a body move underneath (a replay
    /// asked for between turns plays over the planning phase).
    /// </summary>
    public sealed class ReplayTouchBlocker : ITouchBlocker
    {
        private readonly InstantReplay _replay;

        public ReplayTouchBlocker(InstantReplay replay)
        {
            _replay = Guard.NotNull(replay, nameof(replay));
        }

        public bool Blocks(Vec2 screenPixels) => _replay.IsPlaying;
    }
}
