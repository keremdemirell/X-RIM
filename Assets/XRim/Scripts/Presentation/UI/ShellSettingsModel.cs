using System.Collections.Generic;
using XRim.Rules;

namespace XRim.Presentation.UI
{
    /// <summary>Everything the Settings screen shows (Session 18): the handedness choice and the volume sliders, all as words and numbers.</summary>
    public sealed class ShellSettingsModel
    {
        public string Title { get; }
        public string HandednessTitle { get; }
        public string HandednessNote { get; }
        public string RightText { get; }
        public string LeftText { get; }

        /// <summary>The hand the account holds its weapon in now.</summary>
        public Handedness Handedness { get; }

        public string VolumesTitle { get; }
        public IReadOnlyList<ShellVolume> Volumes { get; }
        public string BackText { get; }

        public ShellSettingsModel(string title, string handednessTitle, string handednessNote, string rightText, string leftText,
            Handedness handedness, string volumesTitle, IReadOnlyList<ShellVolume> volumes, string backText)
        {
            Title = title;
            HandednessTitle = handednessTitle;
            HandednessNote = handednessNote;
            RightText = rightText;
            LeftText = leftText;
            Handedness = handedness;
            VolumesTitle = volumesTitle;
            Volumes = volumes;
            BackText = backText;
        }
    }
}
