using UnityEngine;

namespace XRim.DebugTools
{
    /// <summary>
    /// Adds the debug overlay at startup in the Editor and development builds. Nothing in a scene references debug
    /// tools, so release builds (where this assembly does not exist) have no missing scripts.
    /// </summary>
    internal static class DebugToolsInstaller
    {
        private const string RootName = "XRim Debug Tools";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            var root = new GameObject(RootName);
            Object.DontDestroyOnLoad(root);
            root.AddComponent<DebugOverlay>();
        }
    }
}
