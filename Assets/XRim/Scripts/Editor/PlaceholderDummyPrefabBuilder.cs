using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using XRim.Config;
using XRim.Rules.Settings;
using XRim.Simulation;
using XRim.Simulation.Settings;
using XRim.Simulation.Unity2D;

namespace XRim.Editor
{
    /// <summary>
    /// Menu XRim/Setup/Build Placeholder Dummies: builds the placeholder crash-test dummy with six bodies and with ten bodies
    /// (D2 picked ten; six stays selectable) from the live tuning profile, and saves both as prefabs.
    /// Run it again after changing body sizes, the shoulder or arm length, the arena scale or a weapon's length or
    /// thickness; masses, joint limits and pose holding apply without a rebuild.
    /// </summary>
    public static class PlaceholderDummyPrefabBuilder
    {
        private static readonly RagdollSegmentation[] Segmentations = { RagdollSegmentation.SixBodies, RagdollSegmentation.TenBodies };

        public static string PrefabPath(RagdollSegmentation segmentation) =>
            $"{EditorPaths.DummyPrefabsFolder}/PlaceholderDummy{(int)segmentation}.prefab";

        [MenuItem(EditorPaths.MenuSetup + "Build Placeholder Dummies", priority = 5)]
        public static void BuildAll()
        {
            var profile = AssetDatabase.LoadAssetAtPath<TuningProfile>(EditorPaths.TuningProfilePath);
            if (profile == null)
            {
                Debug.LogError("[XRim] No TuningProfile. Run XRim/Setup/Create Default Tuning Assets first.");
                return;
            }

            var issues = new List<string>();
            RulesSettings rules = profile.BuildRulesSettings(issues);
            SimulationSettings simulation = profile.BuildSimulationSettings(issues);
            simulation.Validate(issues);
            foreach (string issue in issues)
            {
                Debug.LogWarning($"[XRim] {issue}", profile);
            }

            RagdollSprites sprites = PlaceholderSprites.LoadOrCreate();
            EditorPaths.EnsureFolder(EditorPaths.DummyPrefabsFolder);

            // Build in a preview scene so the open scene is never touched.
            Scene preview = EditorSceneManager.NewPreviewScene();
            var saved = new List<string>();
            int heldItems = 0;
            try
            {
                foreach (RagdollSegmentation segmentation in Segmentations)
                {
                    var spec = new RagdollBuildSpec(simulation.Ragdoll, rules.Paths, segmentation, profile.ArenaSpace, rules.Weapons, sprites);
                    Ragdoll ragdoll = PlaceholderRagdollBuilder.Build(spec);
                    heldItems = ragdoll.HeldItems.Count;
                    SceneManager.MoveGameObjectToScene(ragdoll.gameObject, preview);
                    string path = PrefabPath(segmentation);
                    PrefabUtility.SaveAsPrefabAsset(ragdoll.gameObject, path, out bool success);
                    if (success) saved.Add(path);
                    else Debug.LogError($"[XRim] Could not save {path}.");
                }
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(preview);
            }

            // No ping: jumping the Project window to the new prefabs renders their previews at once, and twice that preview render was
            // followed by a Direct3D 12 device loss on the designer's laptop (Session 11).
            AssetDatabase.SaveAssets();
            Debug.Log($"[XRim] Built {saved.Count} placeholder dummies with {heldItems} held items each (a body blow holds none):\n{string.Join("\n", saved)}");
        }
    }
}
