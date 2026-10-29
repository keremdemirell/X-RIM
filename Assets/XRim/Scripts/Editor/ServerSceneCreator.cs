using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using XRim.Config;
using XRim.Server;

namespace XRim.Editor
{
    /// <summary>
    /// Menu <c>XRim/Setup/Create Server Scene</c> (Session 19): <c>Assets/XRim/Scenes/Server.unity</c>, holding only the online server's
    /// composition root (<see cref="ServerBootstrap"/>) wired to the tuning profile. The dedicated server builds are made of this scene alone;
    /// it is never in the phone's build list. Press Play with it open to run the server inside the Editor. Running it again rebuilds it.
    /// </summary>
    public static class ServerSceneCreator
    {
        private const string ServerObjectName = "XRimServer";

        [MenuItem(EditorPaths.MenuSetup + "Create Server Scene", priority = 5)]
        public static void Create()
        {
            if (!Build()) return;
            Debug.Log($"[XRim] Created {EditorPaths.ServerScenePath}. Press Play with it open to run the server in the Editor, or build it with " +
                      "XRim/Build/Windows Dedicated Server.");
        }

        /// <summary>Builds and saves the scene; false (with the reason in the Console) when it cannot.</summary>
        public static bool Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[XRim] Stop Play mode before creating the Server scene.");
                return false;
            }

            var tuning = AssetDatabase.LoadAssetAtPath<TuningProfile>(EditorPaths.TuningProfilePath);
            if (tuning == null)
            {
                Debug.LogError($"[XRim] {EditorPaths.TuningProfilePath} does not exist. Run XRim/Setup/Create Default Tuning Assets first.");
                return false;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;

            EditorPaths.EnsureFolder(EditorPaths.ScenesFolder);
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var server = new GameObject(ServerObjectName).AddComponent<ServerBootstrap>();
            var serialized = new SerializedObject(server);
            serialized.FindProperty(ServerBootstrap.FieldNames.Tuning).objectReferenceValue = tuning;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return EditorSceneManager.SaveScene(scene, EditorPaths.ServerScenePath);
        }
    }
}
