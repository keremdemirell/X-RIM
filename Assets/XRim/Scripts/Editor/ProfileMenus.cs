using System.IO;
using UnityEditor;
using UnityEngine;
using XRim.Config;
using XRim.Profile;

namespace XRim.Editor
{
    /// <summary>
    /// XRim → Profile (Session 18). The player's profile (handedness, wallets, what they own, settings) is a file in a folder under
    /// <c>Application.persistentDataPath</c>; Reset Profile erases it so the next Play is a first launch: the handedness pick, the starting
    /// weapons, empty wallets.
    /// </summary>
    internal static class ProfileMenus
    {
        private const string ResetItem = EditorPaths.MenuProfile + "Reset Profile (next Play is a first launch)";

        [MenuItem(ResetItem)]
        private static void ResetProfile()
        {
            string folder = ProfileLocation.DefaultDirectory();
            if (!EditorUtility.DisplayDialog("Reset Profile",
                    "This erases the saved profile in\n" + folder + "\n\nHandedness, Bolts and Gears, everything bought and the settings are gone, " +
                    "and the next Play starts as a first launch. The game's own files are not touched.", "Erase profile", "Cancel"))
            {
                return;
            }

            ProfileStore.DeleteFiles(folder);
            Debug.Log("[XRim] Profile erased. The next Play is a first launch.");
        }

        /// <summary>A running game keeps the profile in memory and saves it again, so a reset during Play would not stick.</summary>
        [MenuItem(ResetItem, true)]
        private static bool CanResetProfile() => !EditorApplication.isPlayingOrWillChangePlaymode;

        [MenuItem(EditorPaths.MenuProfile + "Open Profile Folder")]
        private static void OpenProfileFolder()
        {
            string folder = ProfileLocation.DefaultDirectory();
            Directory.CreateDirectory(folder);
            string file = Path.Combine(folder, ProfileFormat.FileName);
            EditorUtility.RevealInFinder(File.Exists(file) ? file : folder);
        }
    }
}
