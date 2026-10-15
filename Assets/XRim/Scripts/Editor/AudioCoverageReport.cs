using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using XRim.Config;
using XRim.Presentation.Audio;

namespace XRim.Editor
{
    /// <summary>
    /// Menu XRim/Reports/Audio Coverage (Session 13): without changing anything, says in the Console which sounds the game plays (and how
    /// many versions of each), which are still silent (the most important first), the music, every file in
    /// <c>Assets/XRim/Audio/Incoming/</c> the game cannot use and why, and the files added since the sound sets were last built
    /// (<see cref="AudioCoverage"/> writes the words).
    /// </summary>
    internal static class AudioCoverageReport
    {
        [MenuItem(EditorPaths.MenuReports + "Audio Coverage", priority = 23)]
        private static void Report()
        {
            var profile = AssetDatabase.LoadAssetAtPath<TuningProfile>(EditorPaths.TuningProfilePath);
            AudioConfig audio = profile != null ? profile.Audio : null;
            if (audio == null)
            {
                Debug.LogWarning("[XRim] Audio coverage: the tuning profile has no Audio asset yet. Run XRim/Setup/Create Default Tuning Assets " +
                                 "once (it never overwrites anything), then this report again.");
                return;
            }

            EditorPaths.EnsureFolder(EditorPaths.AudioIncomingFolder);
            string folder = Path.GetFullPath(EditorPaths.AudioIncomingFolder);
            List<string> files = Directory.GetFiles(folder).Select(Path.GetFileName).ToList();
            var built = new List<string>();
            int VersionsOf(SoundCue cue) => Collect(audio.Effects, cue, built) + Collect(audio.Grunts, cue, built);
            var counts = AudioCoverage.Priority.ToDictionary(cue => cue, VersionsOf);
            if (audio.Music != null) built.Add(audio.Music.name);

            Debug.Log("[XRim] " + AudioCoverage.Report(cue => counts[cue], audio.Music != null ? audio.Music.name : null, files, built), audio);
        }

        /// <summary>How many clips a set holds for a cue; their names go to the built list.</summary>
        private static int Collect(SoundSetDefinition set, SoundCue cue, List<string> builtNames)
        {
            if (set == null || !set.TryGetClips(cue, out SoundCueClips clips)) return 0;
            int count = 0;
            foreach (AudioClip clip in clips.Clips)
            {
                if (clip == null) continue;
                builtNames.Add(clip.name);
                count++;
            }

            return count;
        }
    }
}
