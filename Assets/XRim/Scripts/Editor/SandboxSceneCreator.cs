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
using XRim.Simulation.Settings;
using XRim.Simulation.Unity2D;
using Object = UnityEngine.Object;

namespace XRim.Editor
{
    /// <summary>
    /// Menu XRim/Setup/Create Sandbox Scene: the match scene, generated (running it again rebuilds it). A camera framing the
    /// whole arena, an arena root the camera director can flip, and the MatchBootstrap composition root wired to the tuning
    /// profile and both placeholder dummy prefabs (built first if missing), in Sandbox mode. Saved to Assets/XRim/Scenes and added first in
    /// the build list. Debug tools install themselves at runtime, so the scene never references them.
    /// </summary>
    public static class SandboxSceneCreator
    {
        private const string MainCameraTag = "MainCamera";

        /// <summary>Frames the 2000-unit arena edge to edge at 16:9, with the floor near the bottom (world units).</summary>
        private const float CameraOrthographicSize = 5.75f;

        private const float CameraHeight = 5.25f;
        private const float CameraZ = -10f;
        private static readonly Color BackgroundColor = new Color(0.09f, 0.1f, 0.12f);

        [MenuItem(EditorPaths.MenuSetup + "Create Sandbox Scene", priority = 3)]
        public static void Create()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(EditorPaths.SandboxScenePath) != null &&
                !EditorUtility.DisplayDialog("XRim", "The Sandbox scene already exists. Rebuild it? (It is generated; nothing in it is hand-made.)",
                    "Rebuild", "Cancel"))
            {
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (AssetDatabase.LoadAssetAtPath<TuningProfile>(EditorPaths.TuningProfilePath) == null)
            {
                Debug.LogError("[XRim] No TuningProfile. Run XRim/Setup/Create Default Tuning Assets first.");
                return;
            }

            if (LoadDummy(RagdollSegmentation.SixBodies) == null || LoadDummy(RagdollSegmentation.TenBodies) == null)
            {
                PlaceholderDummyPrefabBuilder.BuildAll();
            }

            EditorPaths.EnsureFolder(EditorPaths.ScenesFolder);
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Load the assets only now: opening a new scene unloads assets nothing references yet, which would leave
            // references loaded before it pointing at nothing.
            var profile = AssetDatabase.LoadAssetAtPath<TuningProfile>(EditorPaths.TuningProfilePath);
            Ragdoll sixBodies = LoadDummy(RagdollSegmentation.SixBodies);
            Ragdoll tenBodies = LoadDummy(RagdollSegmentation.TenBodies);
            Sprite square = PlaceholderSprites.LoadOrCreate().Square;

            var cameraObject = new GameObject("Main Camera") { tag = MainCameraTag };
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = CameraOrthographicSize;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = BackgroundColor;
            cameraObject.transform.position = new Vector3(0f, CameraHeight, CameraZ);

            var arenaRoot = new GameObject("ArenaRoot");
            var cameraDirector = cameraObject.AddComponent<FixedSideCameraDirector>();
            SetReference(cameraDirector, FixedSideCameraDirector.FieldNames.ArenaRoot, arenaRoot.transform);

            var bootstrapObject = new GameObject("MatchBootstrap");
            var bootstrap = bootstrapObject.AddComponent<MatchBootstrap>();
            bootstrapObject.AddComponent<TouchInputReader>();
            SetReference(bootstrap, MatchBootstrap.FieldNames.Tuning, profile);
            SetReference(bootstrap, MatchBootstrap.FieldNames.SixBodies, sixBodies);
            SetReference(bootstrap, MatchBootstrap.FieldNames.TenBodies, tenBodies);
            SetReference(bootstrap, MatchBootstrap.FieldNames.ArenaRoot, arenaRoot.transform);
            SetReference(bootstrap, MatchBootstrap.FieldNames.Square, square);
            SetMode(bootstrap, MatchMode.Sandbox);

            EditorSceneManager.SaveScene(scene, EditorPaths.SandboxScenePath);
            AddToBuildSettings(EditorPaths.SandboxScenePath);
            Debug.Log($"[XRim] Created {EditorPaths.SandboxScenePath}. Press Play and plan both sides with the mouse (the sandbox panel " +
                      "at the top right lists the keys). Set MatchBootstrap's mode to BotVsBot to watch two bots instead.");
        }

        private static Ragdoll LoadDummy(RagdollSegmentation segmentation)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlaceholderDummyPrefabBuilder.PrefabPath(segmentation));
            return prefab != null ? prefab.GetComponent<Ragdoll>() : null;
        }

        private static void SetReference(Object target, string fieldName, Object value)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(fieldName) ??
                                          throw new InvalidOperationException($"Field '{fieldName}' not found on {target.GetType().Name}.");
            if (value == null) throw new InvalidOperationException($"Nothing to assign to '{fieldName}' on {target.GetType().Name}.");
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetMode(MatchBootstrap bootstrap, MatchMode mode)
        {
            var serialized = new SerializedObject(bootstrap);
            serialized.FindProperty(MatchBootstrap.FieldNames.Mode).enumValueIndex = (int)mode;
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
