using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using XRim.Config;
using XRim.Rules.Settings;

namespace XRim.Editor
{
    /// <summary>
    /// Menu XRim/Setup/Reset Body Moves To Starting Values. Body-move assets keep whatever they were created with, so when a
    /// session changes the starting values (Session 05 reshaped every move), this puts every body-move asset back to
    /// <see cref="GddStartingValues.CreateBodyMoves"/>. It first adds any missing body-move asset to the profile (as Create
    /// Default Tuning Assets does), asks before overwriting, and can be undone.
    /// </summary>
    public static class BodyMoveStartingValuesResetter
    {
        private const string UndoName = "Reset Body Moves To Starting Values";

        [MenuItem(EditorPaths.MenuSetup + "Reset Body Moves To Starting Values", priority = 6)]
        public static void Reset()
        {
            if (!EditorUtility.DisplayDialog(UndoName,
                    "Overwrite every body-move asset (crouch, lunge, step back, jump and the neutral move) with the starting values " +
                    "in the code? Values tuned in these assets are lost (Edit > Undo brings them back).", "Reset", "Cancel"))
            {
                return;
            }

            DefaultTuningAssetsCreator.Create();
            List<BodyMoveStats> defaults = GddStartingValues.CreateBodyMoves();
            var report = new List<string>();
            foreach (string guid in AssetDatabase.FindAssets("t:" + nameof(BodyMoveDefinition)))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<BodyMoveDefinition>(path);
                BodyMoveStats starting = asset != null ? defaults.Find(stats => stats.Move == asset.Settings.Move) : null;
                if (starting == null) continue;

                Undo.RecordObject(asset, UndoName);
                asset.Overwrite(starting);
                EditorUtility.SetDirty(asset);
                report.Add(path);
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[XRim] Reset {report.Count} body-move asset(s) to the starting values:\n{string.Join("\n", report)}");
        }
    }
}
