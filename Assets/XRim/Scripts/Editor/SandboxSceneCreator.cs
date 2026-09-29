using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using XRim.App;
using XRim.Config;
using XRim.Input;
using XRim.Presentation.Cameras;

namespace XRim.Editor
{
    /// <summary>
    /// Menu XRim/Setup/Create Sandbox Scene: a test scene with a camera, an arena root and the MatchBootstrap
    /// composition root, saved to Assets/XRim/Scenes and added first in the build list. Debug tools install
    /// themselves at runtime, so the scene never references them.
    /// </summary>
    public static class SandboxSceneCreator
    {
        private const float CameraOrthographicSize = 6f;
        private const float CameraZ = -10f;
        private const string MainCameraTag = "MainCamera";

        [MenuItem(EditorPaths.MenuSetup + "Create Sandbox Scene", priority = 3)]
        public static void Create()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(EditorPaths.SandboxScenePath) != null)
            {
                if (EditorUtility.DisplayDialog("XRim", "The Sandbox scene already exists. Open it?", "Open", "Cancel") &&
                    EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    EditorSceneManager.OpenScene(EditorPaths.SandboxScenePath);
                }

                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            EditorPaths.EnsureFolder(EditorPaths.ScenesFolder);
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraObject = new GameObject("Main Camera") { tag = MainCameraTag };
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = CameraOrthographicSize;
            cameraObject.transform.position = new Vector3(0f, 0f, CameraZ);

            var arenaRoot = new GameObject("ArenaRoot");
            var cameraDirector = cameraObject.AddComponent<FixedSideCameraDirector>();
            SetReference(cameraDirector, FixedSideCameraDirector.FieldNames.ArenaRoot, arenaRoot.transform);

            var bootstrapObject = new GameObject("MatchBootstrap");
            var bootstrap = bootstrapObject.AddComponent<MatchBootstrap>();
            bootstrapObject.AddComponent<TouchInputReader>();

            var profile = AssetDatabase.LoadAssetAtPath<TuningProfile>(EditorPaths.TuningProfilePath);
            if (profile != null) SetReference(bootstrap, MatchBootstrap.FieldNames.Tuning, profile);
            else Debug.LogWarning("[XRim] No TuningProfile yet: run XRim/Setup/Create Default Tuning Assets, then assign it on MatchBootstrap.");

            EditorSceneManager.SaveScene(scene, EditorPaths.SandboxScenePath);
            AddToBuildSettings(EditorPaths.SandboxScenePath);
            Debug.Log($"[XRim] Created {EditorPaths.SandboxScenePath}. Press Play: the console should report the skeleton is ready.");
        }

        private static void SetReference(UnityEngine.Object target, string fieldName, UnityEngine.Object value)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(fieldName) ??
                                          throw new InvalidOperationException($"Field '{fieldName}' not found on {target.GetType().Name}.");
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AddToBuildSettings(string scenePath)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (scenes.Exists(s => s.path == scenePath)) return;
            scenes.Insert(0, new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
