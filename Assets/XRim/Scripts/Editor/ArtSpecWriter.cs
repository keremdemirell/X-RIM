using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;
using XRim.Config;
using XRim.Presentation.ArtSpec;

namespace XRim.Editor
{
    /// <summary>
    /// Menu XRim/Reports/Write Art Spec (Session 13): writes Docs/ART_SPEC.md from the tuning profile (the dummy's sizes and joints, the
    /// weapons, the effects' sizes, the arena's scale), so the art spec always matches what the game builds. Without a profile it writes
    /// the code's starting values.
    /// </summary>
    public static class ArtSpecWriter
    {
        private const string DocumentPath = "Docs/ART_SPEC.md";

        [MenuItem(EditorPaths.MenuReports + "Write Art Spec", priority = 22)]
        public static void WriteArtSpec()
        {
            var profile = AssetDatabase.LoadAssetAtPath<TuningProfile>(EditorPaths.TuningProfilePath);
            ArtSpecInputs inputs = profile != null ? From(profile) : new ArtSpecInputs();
            inputs.Date = DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            string path = Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? string.Empty, DocumentPath);
            File.WriteAllText(path, ArtSpecDocument.Write(inputs).Replace("\r\n", "\n"));
            Debug.Log($"[XRim] Wrote {DocumentPath} from {inputs.Source}.");
        }

        private static ArtSpecInputs From(TuningProfile profile)
        {
            var issues = new List<string>();
            var inputs = new ArtSpecInputs
            {
                Rules = profile.BuildRulesSettings(issues),
                Simulation = profile.BuildSimulationSettings(issues),
                WorldUnitsPerArenaUnit = profile.ArenaSpace.WorldUnitsPerArenaUnit,
                MaxViewHeightUnits = 2f * MatchSceneBuilder.CameraOrthographicSize / profile.ArenaSpace.WorldUnitsPerArenaUnit,
                Source = "the tuning profile " + EditorPaths.TuningProfilePath,
            };

            FeelConfig feel = profile.Feel;
            if (feel != null)
            {
                inputs.DebrisSizeUnits = feel.DebrisSizeUnits;
                inputs.FluidDropSizeUnits = feel.FluidDropSizeUnits;
                inputs.SparkSizeUnits = feel.SparkSizeUnits;
                inputs.DuelViewHeightUnits = feel.DuelViewHeightUnits;
                inputs.ReplayZoomViewFraction = feel.ReplayZoomViewFraction;
                inputs.FramingFloorMarginUnits = feel.FramingFloorMarginUnits;
                inputs.FramingEdgeMarginUnits = feel.FramingEdgeMarginUnits;
            }

            foreach (string issue in issues)
            {
                Debug.LogWarning($"[XRim] Art spec: {issue}", profile);
            }

            return inputs;
        }
    }
}
