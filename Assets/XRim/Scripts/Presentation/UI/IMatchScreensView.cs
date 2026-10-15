using System;
using System.Collections.Generic;

namespace XRim.Presentation.UI
{
    /// <summary>
    /// The full-screen moments around a match: picking a mode, handing the device over in hot-seat, and the end of the match. At most
    /// one shows at a time. The view only shows text it is given and reports presses.
    /// </summary>
    public interface IMatchScreensView
    {
        /// <summary>The index of the option picked, in the order given to <see cref="ShowModePicker"/>.</summary>
        event Action<int> ModePicked;

        /// <summary>The player on the cover screen tapped it: they have the device.</summary>
        event Action CoverConfirmed;

        event Action RematchPressed;
        event Action ChangeModePressed;

        /// <summary>A tap during the instant replay: skip it.</summary>
        event Action ReplaySkipPressed;

        void ShowModePicker(string title, string note, IReadOnlyList<MenuOption> modes);

        /// <summary>An opaque screen that hides the arena while the device changes hands; a tap anywhere confirms it.</summary>
        void ShowCover(string player, string instruction, string action);

        void ShowMatchOver(string title, string reason, string summary);

        /// <summary>
        /// The instant replay's banner across the top (Session 13); the arena stays fully visible and a tap anywhere skips the replay.
        /// </summary>
        void ShowReplay(string title, string caption, string hint);

        void Hide();
    }
}
