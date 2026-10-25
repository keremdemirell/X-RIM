using System.Collections.Generic;

namespace XRim.Presentation.UI
{
    /// <summary>
    /// Everything a tabbed list screen shows (Session 18): the Loadout collection and the Shop are both this, with different words and
    /// buttons. Tabs of rows, one tab open.
    /// </summary>
    public sealed class ShellCollectionModel
    {
        public string Title { get; }
        public IReadOnlyList<ShellCollectionTab> Tabs { get; }
        public int SelectedTab { get; }
        public string BackText { get; }

        /// <summary>A line next to the title, for example the player's balances; empty: none.</summary>
        public string Header { get; }

        /// <summary>What the last press did ("BOUGHT: SWORD", "Not enough Bolts"); empty: none.</summary>
        public string Status { get; }

        public ShellCollectionModel(string title, IReadOnlyList<ShellCollectionTab> tabs, int selectedTab, string backText, string header = "",
            string status = "")
        {
            Title = title;
            Tabs = tabs;
            SelectedTab = selectedTab;
            BackText = backText;
            Header = header ?? string.Empty;
            Status = status ?? string.Empty;
        }
    }
}
