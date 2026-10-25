namespace XRim.Presentation.UI
{
    /// <summary>One volume slider on the Settings screen.</summary>
    public readonly struct ShellVolume
    {
        public string Label { get; }

        /// <summary>0 (silent) to 1 (the mix as the designer set it).</summary>
        public float Fraction { get; }

        public ShellVolume(string label, float fraction)
        {
            Label = label;
            Fraction = fraction;
        }
    }
}
