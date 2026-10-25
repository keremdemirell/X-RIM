namespace XRim.Presentation.UI
{
    /// <summary>
    /// One line of the Loadout collection and of the Shop (Session 18): a weapon, a signature move or a look. It can have up to two buttons
    /// at its right end (a look can be bought with either currency); a button with no text is not drawn, but keeps its index.
    /// </summary>
    public readonly struct ShellCollectionRow
    {
        public string Title { get; }
        public string Detail { get; }

        /// <summary>Not owned yet: shown dimmed.</summary>
        public bool Locked { get; }

        /// <summary>The first button ("EQUIP", "300 BOLTS"); empty: none.</summary>
        public string ButtonText { get; }

        public bool ButtonEnabled { get; }

        /// <summary>The second button ("50 GEARS"); empty: none.</summary>
        public string SecondButtonText { get; }

        public bool SecondButtonEnabled { get; }

        public ShellCollectionRow(string title, string detail, bool locked, string buttonText = "", bool buttonEnabled = true,
            string secondButtonText = "", bool secondButtonEnabled = true)
        {
            Title = title;
            Detail = detail;
            Locked = locked;
            ButtonText = buttonText ?? string.Empty;
            ButtonEnabled = buttonEnabled;
            SecondButtonText = secondButtonText ?? string.Empty;
            SecondButtonEnabled = secondButtonEnabled;
        }
    }
}
