using UnityEngine;
using UnityEngine.UIElements;

namespace XRim.Presentation.UI
{
    /// <summary>
    /// The panel settings every HUD document uses: they scale with the screen height from a reference resolution, so a wider screen
    /// shows more but nothing grows (like the arena view). They are a copy of a PanelSettings asset made in the Editor
    /// (<c>XRim/Setup/Create HUD Panel Settings</c>) when there is one: UI Toolkit's text needs ICU data, which Unity assigns to
    /// PanelSettings assets (in the Editor and in a build) but never to PanelSettings created in code, so in a player build a HUD made
    /// without a template draws no text. Without a template (tests, the Editor) they are created in code. The caller owns the result
    /// and destroys it.
    /// </summary>
    public static class HudPanelSettings
    {
        public static PanelSettings Create(Vector2Int referenceResolution, int sortingOrder, PanelSettings template)
        {
            PanelSettings settings = template != null ? Object.Instantiate(template) : ScriptableObject.CreateInstance<PanelSettings>();
            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = referenceResolution;
            settings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            settings.match = 1f; // by height
            settings.sortingOrder = sortingOrder;
            return settings;
        }
    }
}
