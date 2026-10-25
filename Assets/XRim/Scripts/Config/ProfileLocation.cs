using System.IO;
using UnityEngine;

namespace XRim.Config
{
    /// <summary>Where the game keeps the player's profile on this device (Session 18): a folder under <c>Application.persistentDataPath</c>.</summary>
    public static class ProfileLocation
    {
        public const string FolderName = "XRimProfile";

        /// <summary>
        /// On Windows (the Editor) it is under <c>%USERPROFILE%/AppData/LocalLow/&lt;company&gt;/&lt;product&gt;</c>; on Android under the app's
        /// own <c>files</c> folder, which Android removes with the app (so uninstalling gives a fresh first launch).
        /// </summary>
        public static string DefaultDirectory() => Path.Combine(Application.persistentDataPath, FolderName);
    }
}
