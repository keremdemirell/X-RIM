using System;
using System.Collections.Generic;
using XRim.Rules;

namespace XRim.Presentation.UI
{
    /// <summary>
    /// The screens of the game outside a match (Session 18): the first-launch handedness pick, the menus and Settings. At most one shows at
    /// a time. The view only shows the words and numbers it is given and reports presses; the flow lives in App's <c>MetaShell</c>.
    /// </summary>
    public interface IShellView
    {
        /// <summary>A menu option was pressed: its index in the list last shown.</summary>
        event Action<int> OptionPicked;

        /// <summary>The back button of a menu or of Settings.</summary>
        event Action BackPressed;

        /// <summary>The first-launch pick, or the choice made in Settings (GDD §4).</summary>
        event Action<Handedness> HandednessPicked;

        /// <summary>A volume's minus (-1) or plus (+1) was pressed: the slider's index in the list last shown.</summary>
        event Action<int, int> VolumeStepped;

        /// <summary>A tab of the Loadout collection was pressed: its index in the list last shown.</summary>
        event Action<int> CollectionTabSelected;

        /// <summary>A button of a row was pressed: the tab, the row in it and which button (0 first, 1 second).</summary>
        event Action<int, int, int> CollectionRowPressed;

        /// <summary>A tabbed list (Session 18): the Loadout collection of what the player owns, and the Shop.</summary>
        void ShowCollection(ShellCollectionModel model);

        /// <summary>The first-launch question (GDD §4, Decided): right-handed or left-handed.</summary>
        void ShowHandednessPick(string title, string note, string rightText, string leftText);

        /// <summary>A menu of big buttons, with an optional back button (<paramref name="backText"/> empty: none).</summary>
        void ShowMenu(string title, string note, IReadOnlyList<MenuOption> options, string backText);

        void ShowSettings(ShellSettingsModel model);

        void Hide();
    }
}
