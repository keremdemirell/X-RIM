namespace XRim.Presentation.UI
{
    /// <summary>One big button on a menu screen: a title and a line saying what it does.</summary>
    public readonly struct MenuOption
    {
        public string Title { get; }
        public string Detail { get; }

        /// <summary>False: shown dimmed and cannot be pressed (something that is not built yet).</summary>
        public bool Enabled { get; }

        public MenuOption(string title, string detail, bool enabled = true)
        {
            Title = title;
            Detail = detail;
            Enabled = enabled;
        }
    }
}
