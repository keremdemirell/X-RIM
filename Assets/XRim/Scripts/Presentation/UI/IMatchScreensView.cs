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

        /// <summary>A weapon card of the loadout picker was tapped: its index in the choices last shown.</summary>
        event Action<int> LoadoutChoiceTapped;

        /// <summary>The lock-in button of the loadout picker was pressed (it is only pressable when the picks are valid).</summary>
        event Action LoadoutConfirmed;

        /// <summary>A tap on the loadout reveal: start the match.</summary>
        event Action LoadoutRevealConfirmed;

        void ShowModePicker(string title, string note, IReadOnlyList<MenuOption> modes);

        /// <summary>
        /// The pre-match loadout picker (GDD §6): one card per weapon (picked ones numbered by slot) and a lock-in button. Calling it
        /// again with the same number of cards updates what is shown without rebuilding it.
        /// </summary>
        void ShowLoadoutPicker(string title, string note, IReadOnlyList<LoadoutChoice> choices, string confirmText, bool canConfirm);

        /// <summary>Both loadouts side by side, shown to both players at the start of the match (GDD §6, Decided).</summary>
        void ShowLoadoutReveal(string title, string leftName, IReadOnlyList<string> leftWeapons, string rightName,
            IReadOnlyList<string> rightWeapons, string action);

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
