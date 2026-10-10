namespace XRim.Presentation.UI
{
    /// <summary>One big button on a menu screen: a title and a line saying what it does.</summary>
    public readonly struct MenuOption
    {
        public string Title { get; }
        public string Detail { get; }

        public MenuOption(string title, string detail)
        {
            Title = title;
            Detail = detail;
        }
    }
}
