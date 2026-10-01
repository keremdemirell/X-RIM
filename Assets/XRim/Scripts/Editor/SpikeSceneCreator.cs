using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using XRim.Config;
using XRim.DebugTools.Spike;
using XRim.Simulation.Settings;
using XRim.Simulation.Unity2D;
using Object = UnityEngine.Object;

namespace XRim.Editor
{
    /// <summary>
    /// Menu XRim/Spike/Create Spike Scene (Session 02 feel spike): a camera and the spike harness, wired to the tuning
    /// profile and both placeholder dummy prefabs (built first if missing). The scene is generated, so running the menu
    /// again rebuilds it. It is a development tool and is never added to the build list.
    /// </summary>
    public static class SpikeSceneCreator
    {
        private const string MainCameraTag = "MainCamera";

        /// <summary>Frames both dummies, the floor and paths drawn up to the reach limit (world units).</summary>
        private const float CameraOrthographicSize = 4.5f;

        private const float CameraHeight = 3f;
        private const float CameraZ = -10f;
        private static readonly Color BackgroundColor = new Color(0.09f, 0.1f, 0.12f);

        [MenuItem(EditorPaths.MenuSpike + "Create Spike Scene", priority = 41)]
        public static void Create()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(EditorPaths.SpikeScenePath) != null &&
                !EditorUtility.DisplayDialog("XRim", "The spike scene already exists. Rebuild it? (It is generated; nothing in it is hand-made.)",
                    "Rebuild", "Cancel"))
            {
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var profile = AssetDatabase.LoadAssetAtPath<TuningProfile>(EditorPaths.TuningProfilePath);
            if (profile == null)
            {
                Debug.LogError("[XRim] No TuningProfile. Run XRim/Setup/Create Default Tuning Assets first.");
                return;
            }

            Ragdoll sixBodies = LoadDummy(RagdollSegmentation.SixBodies);
            Ragdoll tenBodies = LoadDummy(RagdollSegmentation.TenBodies);
            if (sixBodies == null || tenBodies == null)
            {
                PlaceholderDummyPrefabBuilder.BuildAll();
                sixBodies = LoadDummy(RagdollSegmentation.SixBodies);
                tenBodies = LoadDummy(RagdollSegmentation.TenBodies);
            }

            EditorPaths.EnsureFolder(EditorPaths.ScenesFolder);
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraObject = new GameObject("Main Camera") { tag = MainCameraTag };
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = CameraOrthographicSize;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = BackgroundColor;
            cameraObject.transform.position = new Vector3(0f, CameraHeight, CameraZ);

            var harnessObject = new GameObject("SpikeHarness");
            var harness = harnessObject.AddComponent<SpikeHarness>();
            SetReference(harness, SpikeHarness.FieldNames.Tuning, profile);
            SetReference(harness, SpikeHarness.FieldNames.Camera, camera);
            SetReference(harness, SpikeHarness.FieldNames.SixBodies, sixBodies);
            SetReference(harness, SpikeHarness.FieldNames.TenBodies, tenBodies);
            SetReference(harness, SpikeHarness.FieldNames.Square, PlaceholderSprites.LoadOrCreate().Square);

            EditorSceneManager.SaveScene(scene, EditorPaths.SpikeScenePath);
            Debug.Log($"[XRim] Created {EditorPaths.SpikeScenePath}. Press Play, drag with the left mouse button to draw a path, " +
                      "then press Space to swing. The keys are listed at the bottom left.");
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
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
