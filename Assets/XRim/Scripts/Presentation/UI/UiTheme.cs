using UnityEngine;

namespace XRim.Presentation.UI
{
    /// <summary>
    /// The one look the full-screen menus share (Session 18; placeholder until the UI art, Session 17): the dark screen, the yellow accent for
    /// titles, green for "go" and "chosen", dimmed for what cannot be pressed, and the gap between things. The text colours and the plain button
    /// live in <see cref="HudElements"/>, which every HUD shares. Change a value here and every menu changes with it.
    /// </summary>
    internal static class UiTheme
    {
        /// <summary>The distance between stacked things on a menu screen, in reference-screen units.</summary>
        public const float Gap = 18f;

        /// <summary>The opaque background of a screen that hides the arena.</summary>
        public static readonly Color ScreenColor = new Color(0.07f, 0.08f, 0.1f, 1f);

        /// <summary>A see-through darkening over the arena (the match-over screen).</summary>
        public static readonly Color DimmedScreenColor = new Color(0f, 0f, 0f, 0.6f);

        /// <summary>The big "go" buttons (rematch, lock in, start).</summary>
        public static readonly Color ConfirmColor = new Color(0.2f, 0.7f, 0.3f, 1f);

        /// <summary>A card or button that is picked or switched on (the chosen hand, a picked weapon).</summary>
        public static readonly Color ChosenColor = new Color(0.1f, 0.38f, 0.17f, 0.95f);

        /// <summary>Text and edges of what cannot be pressed or is not owned yet.</summary>
        public static readonly Color DisabledColor = new Color(1f, 1f, 1f, 0.3f);

        /// <summary>The empty part of a volume bar.</summary>
        public static readonly Color BarTrackColor = new Color(1f, 1f, 1f, 0.15f);
    }
}
