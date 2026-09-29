using UnityEditor;
using UnityEngine;

namespace XRim.Editor
{
    /// <summary>
    /// Menu XRim/Setup/Apply Project Settings. Landscape only (GDD §4: landscape is assumed, TBD) and Physics 2D in
    /// Script mode: gameplay physics runs in its own manually stepped scene, and cosmetic physics is stepped by
    /// playback so slow motion slows it too. Safe to run again.
    /// </summary>
    public static class ProjectSettingsApplier
    {
        private const string Physics2DSettingsPath = "ProjectSettings/Physics2DSettings.asset";
        private const string SimulationModeProperty = "m_SimulationMode";

        [MenuItem(EditorPaths.MenuSetup + "Apply Project Settings", priority = 2)]
        public static void Apply()
        {
            ApplyLandscapeOrientation();
            bool physicsApplied = ApplyScriptPhysics2DStepping();
            AssetDatabase.SaveAssets();
            Debug.Log("[XRim] Project settings applied: landscape left/right only; Physics 2D simulation mode " +
                      (physicsApplied ? "= Script." : "NOT changed (see warning)."));
        }

        private static void ApplyLandscapeOrientation()
        {
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
        }

        private static bool ApplyScriptPhysics2DStepping()
        {
            Physics2D.simulationMode = SimulationMode2D.Script;

            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(Physics2DSettingsPath);
            if (assets.Length == 0)
            {
                Debug.LogWarning($"[XRim] Could not load {Physics2DSettingsPath}. Set Project Settings > Physics 2D > Simulation Mode to Script by hand.");
                return false;
            }

            var settings = new SerializedObject(assets[0]);
            SerializedProperty mode = settings.FindProperty(SimulationModeProperty);
            if (mode == null)
            {
                Debug.LogWarning($"[XRim] '{SimulationModeProperty}' not found. Set Project Settings > Physics 2D > Simulation Mode to Script by hand.");
                return false;
            }

            mode.intValue = (int)SimulationMode2D.Script;
            settings.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }
    }
}
