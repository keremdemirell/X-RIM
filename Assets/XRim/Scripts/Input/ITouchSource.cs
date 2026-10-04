using System.Collections.Generic;
using XRim.Core;

namespace XRim.Input
{
    /// <summary>Where touches come from: the Input System on a device (and the mouse in the Editor), or a script in a test.</summary>
    public interface ITouchSource
    {
        /// <summary>The window's current size in pixels (it can change while playing).</summary>
        Vec2 ScreenSizePixels { get; }

        /// <summary>
        /// Appends every reading that arrived since the last call, oldest first, including the ones between frames, so a
        /// slow frame does not thin a stroke out (GDD §4: the touch refresh rate must not change an outcome).
        /// </summary>
        void ReadInto(List<TouchSample> samples);
    }
}
