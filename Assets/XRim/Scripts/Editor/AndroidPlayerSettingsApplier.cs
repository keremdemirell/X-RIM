using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using XRim.Core.Gdd;

namespace XRim.Editor
{
    /// <summary>
    /// The Android player settings a device build needs (Session 10), applied by <c>XRim/Setup/Apply Project Settings</c> and before
    /// every <c>XRim/Build/Android…</c> build. Safe to run again: it only changes what differs and says what it changed.
    /// Landscape is applied by <see cref="ProjectSettingsApplier"/> (GDD §4: landscape is assumed, TBD).
    /// <list type="bullet">
    /// <item>IL2CPP and ARM64: the Mono variation of the Android player ships only 32-bit libraries, so ARM64 needs IL2CPP (also what
    /// Google Play requires), and Mono timings would not be the shipping numbers (GDD §18 performance budget).</item>
    /// <item>Fullscreen, rendering outside the safe area (the HUDs inset themselves by <c>Screen.safeArea</c>) and optimized frame
    /// pacing (Swappy).</item>
    /// <item>A valid Android package name (Unity derives one; see <see cref="DevelopmentApplicationIdentifier"/>).</item>
    /// </list>
    /// </summary>
    internal static class AndroidPlayerSettingsApplier
    {
        /// <summary>
        /// Fallback package name, used only if the stored one is not a valid Android package name. Unity normally derives a valid one
        /// from the company and product names (today "com.DefaultCompany.XRIM"). The real name waits for the store name and trademark
        /// check (GDD §0, TBD; Session 22).
        /// </summary>
        [Placeholder("Store name and trademark are TBD (GDD §0): a development package name until Session 22")]
        public const string DevelopmentApplicationIdentifier = "com.DefaultCompany.XRim";

        // AndroidWindowInsetsType flags: 0 is no inset type, so no system bar is requested visible.
        private const int NoVisibleInsets = 0;

        // Android package name: two or more dot-separated segments, each starting with a letter, then letters, digits or underscores.
        private static readonly Regex ValidApplicationIdentifier = new Regex(@"^[A-Za-z][A-Za-z0-9_]*(\.[A-Za-z][A-Za-z0-9_]*)+$");

        public static bool IsValidApplicationIdentifier(string identifier) =>
            !string.IsNullOrEmpty(identifier) && ValidApplicationIdentifier.IsMatch(identifier);

        /// <summary>Applies the settings and returns one line per change (empty when everything was already right).</summary>
        public static List<string> Apply()
        {
            var changes = new List<string>();
            NamedBuildTarget android = NamedBuildTarget.Android;

            if (PlayerSettings.GetScriptingBackend(android) != ScriptingImplementation.IL2CPP)
            {
                PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
                changes.Add("scripting backend = IL2CPP");
            }

            if (PlayerSettings.Android.targetArchitectures != AndroidArchitecture.ARM64)
            {
                PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
                changes.Add("target architecture = ARM64");
            }

            // Fullscreen: no system bar (status, navigation) visible at start. (startInFullscreen is obsolete in Unity 6.5.)
            if ((int)PlayerSettings.Android.requestedVisibleInsets != NoVisibleInsets)
            {
                PlayerSettings.Android.requestedVisibleInsets = (AndroidWindowInsetsType)NoVisibleInsets;
                changes.Add("no system bars visible at start (fullscreen)");
            }

            if (!PlayerSettings.Android.renderOutsideSafeArea)
            {
                PlayerSettings.Android.renderOutsideSafeArea = true;
                changes.Add("render outside the safe area");
            }

            if (!PlayerSettings.Android.optimizedFramePacing)
            {
                PlayerSettings.Android.optimizedFramePacing = true;
                changes.Add("optimized frame pacing");
            }

            string identifier = PlayerSettings.GetApplicationIdentifier(android);
            if (!IsValidApplicationIdentifier(identifier))
            {
                PlayerSettings.SetApplicationIdentifier(android, DevelopmentApplicationIdentifier);
                changes.Add($"package name '{identifier}' is not a valid Android package name: now {DevelopmentApplicationIdentifier}");
            }

            return changes;
        }
    }
}
