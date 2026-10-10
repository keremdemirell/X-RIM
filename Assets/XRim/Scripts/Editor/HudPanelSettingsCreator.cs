using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace XRim.Editor
{
    /// <summary>
    /// Menu XRim/Setup/Create HUD Panel Settings (Session 10): the one PanelSettings asset the HUDs copy their own panel settings from
    /// (<c>HudElements.CreateDocument</c>). UI Toolkit's text needs ICU data, which Unity assigns to PanelSettings assets (in the Editor and
    /// in the build) but never to PanelSettings created in code, so in a player build the HUDs drew no text at all. The asset is empty
    /// on purpose: scale mode, reference resolution and sorting are set by the HUD code from <c>InputConfig</c>. Never overwrites an
    /// existing asset; <see cref="MatchSceneBuilder"/> creates it when missing and wires it to the MatchBootstrap.
    /// </summary>
    public static class HudPanelSettingsCreator
    {
        [MenuItem(EditorPaths.MenuSetup + "Create HUD Panel Settings", priority = 4)]
        public static void Create()
        {
            bool existed = AssetDatabase.LoadAssetAtPath<PanelSettings>(EditorPaths.HudPanelSettingsPath) != null;
            LoadOrCreate();
            Debug.Log(existed
                ? $"[XRim] {EditorPaths.HudPanelSettingsPath} already exists (left as it is)."
                : $"[XRim] Created {EditorPaths.HudPanelSettingsPath}. Run XRim/Setup/Create Match Scene so the Match scene references it " +
                  "(a build includes it, and Unity adds the text data to it).");
        }

        /// <summary>The HUD panel settings asset, created first when missing.</summary>
        internal static PanelSettings LoadOrCreate()
        {
            var existing = AssetDatabase.LoadAssetAtPath<PanelSettings>(EditorPaths.HudPanelSettingsPath);
            if (existing != null) return existing;

            EditorPaths.EnsureFolder(EditorPaths.UiFolder);
            var created = ScriptableObject.CreateInstance<PanelSettings>();
            AssetDatabase.CreateAsset(created, EditorPaths.HudPanelSettingsPath);
            AssetDatabase.SaveAssets();
            return created;
        }
    }
}
