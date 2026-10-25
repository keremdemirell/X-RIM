using System.Collections.Generic;

namespace XRim.Presentation.UI
{
    /// <summary>One page of the Loadout collection: its tab label, a line of words above the rows, and the rows.</summary>
    public sealed class ShellCollectionTab
    {
        public string Label { get; }
        public string Note { get; }
        public IReadOnlyList<ShellCollectionRow> Rows { get; }

        public ShellCollectionTab(string label, string note, IReadOnlyList<ShellCollectionRow> rows)
        {
            Label = label;
            Note = note;
            Rows = rows;
        }
    }
}
