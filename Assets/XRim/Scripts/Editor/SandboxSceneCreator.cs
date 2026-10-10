using UnityEditor;
using UnityEngine;
using XRim.App;

namespace XRim.Editor
{
    /// <summary>
    /// Menu XRim/Setup/Create Sandbox Scene: the debug sandbox, a match scene (<see cref="MatchSceneBuilder"/>) whose MatchBootstrap
    /// runs in Sandbox mode, so the debug tools plan both sides. Saved to Assets/XRim/Scenes and added to the build list (after the
    /// Match scene). Running it again rebuilds it.
    /// </summary>
    public static class SandboxSceneCreator
    {
        [MenuItem(EditorPaths.MenuSetup + "Create Sandbox Scene", priority = 3)]
        public static void Create()
        {
            if (!MatchSceneBuilder.Build(EditorPaths.SandboxScenePath, "Sandbox", MatchMode.Sandbox, false, false)) return;

            Debug.Log($"[XRim] Created {EditorPaths.SandboxScenePath}. Press Play and plan both sides with the mouse (the sandbox panel " +
                      "at the top right lists the keys). XRim/Setup/Create Match Scene builds the scene that plays real matches.");
        }
    }
}
