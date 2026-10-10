using UnityEngine;
using XRim.Core;

namespace XRim.Presentation.UI
{
    /// <summary>
    /// Watches <c>Screen.safeArea</c> for one HUD (GDD §4: a phone's notch, camera cut-out and rounded corners must not hide a control).
    /// It gives the insets in the units of a UI that scales with the screen height from a reference resolution, and says when they
    /// changed (the phone turned from landscape-left to landscape-right, or the window was resized), so the HUD only re-lays out then.
    /// </summary>
    internal sealed class HudSafeArea
    {
        private readonly Vector2Int _referenceResolution;
        private ScreenInsets _applied;
        private bool _hasApplied;

        public HudSafeArea(Vector2Int referenceResolution)
        {
            _referenceResolution = referenceResolution;
        }

        /// <summary>The insets now, in panel units (the HUD's reference-resolution pixels).</summary>
        public ScreenInsets Current()
        {
            Rect safe = Screen.safeArea;
            ScreenInsets pixels = ScreenInsets.FromSafeArea(safe.x, safe.y, safe.width, safe.height, Screen.width, Screen.height);

            // The panel scales with the screen height, so one reference pixel is Screen.height / reference height screen pixels.
            float unitsPerPixel = Screen.height > 0 ? _referenceResolution.y / (float)Screen.height : 1f;
            return pixels.Scaled(unitsPerPixel);
        }

        /// <summary>True when the insets differ from the ones this last returned (always on the first call).</summary>
        public bool Changed(out ScreenInsets insets)
        {
            insets = Current();
            if (_hasApplied && insets.Equals(_applied)) return false;

            _applied = insets;
            _hasApplied = true;
            return true;
        }
    }
}
