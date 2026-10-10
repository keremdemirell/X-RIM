using UnityEditor;
using UnityEngine;
using XRim.App;

namespace XRim.Editor
{
    /// <summary>
    /// Menu XRim/Setup/Create Match Scene: the first playable (Session 09). A match scene (<see cref="MatchSceneBuilder"/>) that opens
    /// on the mode picker: hot-seat, against the random debug bot, or two bots to watch. Saved to Assets/XRim/Scenes and put first in
    /// the build list, so a build starts there. Running it again rebuilds it.
    /// </summary>
    public static class MatchSceneCreator
    {
        [MenuItem(EditorPaths.MenuSetup + "Create Match Scene", priority = 3)]
        public static void Create()
        {
            if (!MatchSceneBuilder.Build(EditorPaths.MatchScenePath, "Match", MatchMode.HumanVsBot, true, true)) return;

            Debug.Log($"[XRim] Created {EditorPaths.MatchScenePath} (first in the build list). Press Play and pick a mode. The mouse is a " +
                      "finger: drag in the arena to draw, flick in the strip at your screen edge for a body move, press READY.");
        }
    }
}
