using System.Collections.Generic;
using XRim.Core;
using XRim.Input;

namespace XRim.App
{
    /// <summary>
    /// Every place on the screen that belongs to something other than the arena (the planning HUD, the debug overlay): a touch that
    /// lands on any of them is a button press, never a stroke or a swipe.
    /// </summary>
    public sealed class TouchBlockerSet : ITouchBlocker
    {
        private readonly List<ITouchBlocker> _blockers = new List<ITouchBlocker>();

        public void Add(ITouchBlocker blocker)
        {
            Guard.NotNull(blocker, nameof(blocker));
            if (!_blockers.Contains(blocker)) _blockers.Add(blocker);
        }

        public void Remove(ITouchBlocker blocker) => _blockers.Remove(blocker);

        public bool Blocks(Vec2 screenPixels)
        {
            foreach (ITouchBlocker blocker in _blockers)
            {
                if (blocker.Blocks(screenPixels)) return true;
            }

            return false;
        }
    }
}
