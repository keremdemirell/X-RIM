using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
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
    /// Builds a match scene in code (nothing in it is hand-made, so it is rebuilt rather than edited): a camera framing the whole arena
    /// with the fixed-side camera director, an arena root the director can flip, and the MatchBootstrap composition root wired to the
    /// tuning profile and both placeholder dummy prefabs (built first if missing), with the touch reader. Debug tools install themselves
    /// at runtime, so the scene never references them.
    /// </summary>
    internal static class MatchSceneBuilder
    {
        private const string MainCameraTag = "MainCamera";

        /// <summary>Frames the 2000-unit arena edge to edge at 16:9, with the floor near the bottom (world units).</summary>
        internal const float CameraOrthographicSize = 5.75f;

        private const float CameraHeight = 5.25f;
        private const float CameraZ = -10f;
        private static readonly Color BackgroundColor = new Color(0.09f, 0.1f, 0.12f);

        /// <summary>Builds and saves the scene. False when the user cancelled or something it needs is missing.</summary>
        /// <param name="startup">What the scene does at Play (the Match scene shows the menus, the sandbox waits for its tools).</param>
        /// <param name="first">Put the scene first in the build list (the scene a build starts in).</param>
        public static bool Build(string scenePath, string sceneName, MatchMode mode, MatchStartup startup, bool first)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) != null &&
                !EditorUtility.DisplayDialog("XRim", $"The {sceneName} scene already exists. Rebuild it? (It is generated; nothing in it is hand-made.)",
                    "Rebuild", "Cancel"))
            {
                return false;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;
            if (AssetDatabase.LoadAssetAtPath<TuningProfile>(EditorPaths.TuningProfilePath) == null)
            {
                Debug.LogError("[XRim] No TuningProfile. Run XRim/Setup/Create Default Tuning Assets first.");
                return false;
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
            PanelSettings hudPanelSettings = HudPanelSettingsCreator.LoadOrCreate();

            var cameraObject = new GameObject("Main Camera") { tag = MainCameraTag };
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = CameraOrthographicSize;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = BackgroundColor;
            cameraObject.transform.position = new Vector3(0f, CameraHeight, CameraZ);
            cameraObject.AddComponent<AudioListener>();

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
            SetReference(bootstrap, MatchBootstrap.FieldNames.HudPanelSettings, hudPanelSettings);
            SetReference(bootstrap, MatchBootstrap.FieldNames.Camera, camera);
            SetReference(bootstrap, MatchBootstrap.FieldNames.CameraDirector, cameraDirector);
            var serialized = new SerializedObject(bootstrap);
            serialized.FindProperty(MatchBootstrap.FieldNames.Mode).enumValueIndex = (int)mode;
            serialized.FindProperty(MatchBootstrap.FieldNames.Startup).enumValueIndex = (int)startup;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, scenePath);
            AddToBuildSettings(scenePath, first);
            return true;
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

        /// <summary>Adds the scene to the build list; <paramref name="first"/> also moves it to the top.</summary>
        private static void AddToBuildSettings(string scenePath, bool first)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            int index = scenes.FindIndex(s => s.path == scenePath);
            if (index >= 0 && (!first || index == 0)) return;
            if (index >= 0) scenes.RemoveAt(index);

            var entry = new EditorBuildSettingsScene(scenePath, true);
            if (first) scenes.Insert(0, entry);
            else scenes.Add(entry);
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
