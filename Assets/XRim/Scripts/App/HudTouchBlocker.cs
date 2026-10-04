using UnityEngine;
using XRim.Core;
using XRim.Input;
using XRim.Presentation.UI;

namespace XRim.App
{
    /// <summary>A touch that lands on one of the planning HUD's controls is a button press: the input scheme must not draw or swipe with it.</summary>
    public sealed class HudTouchBlocker : ITouchBlocker
    {
        private readonly PlanningHud _hud;

        public HudTouchBlocker(PlanningHud hud)
        {
            _hud = Guard.NotNull(hud, nameof(hud));
        }

        public bool Blocks(Vec2 screenPixels) => _hud != null && _hud.IsOverControl(new Vector2(screenPixels.X, screenPixels.Y));
    }
}
