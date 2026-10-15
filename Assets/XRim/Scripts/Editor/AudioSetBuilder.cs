using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using XRim.Config;
using XRim.Presentation.Audio;

namespace XRim.Editor
{
    /// <summary>
    /// XRim → Setup → Build Audio Sets From Incoming (Session 13): sorts the sound files in <c>Assets/XRim/Audio/Incoming/</c> by their
    /// names (<see cref="AudioFileNaming"/>: a cue's snake-case name, then anything after an underscore; <c>grunt_…</c> to the grunt pack;
    /// <c>music…</c> the loop) into the default effects set and grunt pack (<c>Assets/XRim/Data/Audio/</c>), and puts them and the music in
    /// the tuning profile's Audio asset. The folder is the source: running it again replaces each cue's clips with what the folder holds
    /// (a cue's volume set by hand is kept). Safe to run with an empty folder: everything stays silent. Reports what matched, what did not
    /// (with a suggestion for a misspelt name), and which cues still have no sound. XRim → Setup → Open Sound Folder (Session 13 batch 7)
    /// shows the folder in the file explorer.
    /// </summary>
    internal static class AudioSetBuilder
    {
        private const string EffectsPath = EditorPaths.AudioDataFolder + "/Effects.asset";
        private const string GruntsPath = EditorPaths.AudioDataFolder + "/Grunts.asset";
        private const string IncomingReadMe = EditorPaths.AudioIncomingFolder + "/README.md";

        /// <summary>Opens the sound folder in the file explorer (Windows Explorer, or Finder on a Mac), so files can be copied in.</summary>
        [MenuItem(EditorPaths.MenuSetup + "Open Sound Folder")]
        private static void OpenSoundFolder()
        {
            EditorPaths.EnsureFolder(EditorPaths.AudioIncomingFolder);
            string target = File.Exists(IncomingReadMe) ? IncomingReadMe : EditorPaths.AudioIncomingFolder;
            EditorUtility.RevealInFinder(Path.GetFullPath(target));
        }

        [MenuItem(EditorPaths.MenuSetup + "Build Audio Sets From Incoming")]
        private static void Build()
        {
            var profile = AssetDatabase.LoadAssetAtPath<TuningProfile>(EditorPaths.TuningProfilePath);
            if (profile == null || profile.Audio == null)
            {
                Debug.LogError("[XRim] No tuning profile with an Audio asset. Run XRim/Setup/Create Default Tuning Assets first.");
                return;
            }

            EditorPaths.EnsureFolder(EditorPaths.AudioIncomingFolder);
            EditorPaths.EnsureFolder(EditorPaths.AudioDataFolder);

            var effects = new Dictionary<SoundCue, List<AudioClip>>();
            var grunts = new Dictionary<SoundCue, List<AudioClip>>();
            var unmatched = new List<string>();
            AudioClip music = null;
            int clipCount = 0;
            foreach (string path in ClipPaths())
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (clip == null) continue;
                clipCount++;
                if (AudioFileNaming.IsMusic(path))
                {
                    if (music == null) music = clip;
                    continue;
                }

                if (!AudioFileNaming.TryParse(path, out SoundCue cue))
                {
                    unmatched.Add(path);
                    continue;
                }

                Dictionary<SoundCue, List<AudioClip>> into = SoundCues.IsVoice(cue) ? grunts : effects;
                if (!into.TryGetValue(cue, out List<AudioClip> clips)) into[cue] = clips = new List<AudioClip>();
                clips.Add(clip);
            }

            SoundSetDefinition effectsSet = LoadOrCreate(EffectsPath);
            SoundSetDefinition gruntPack = LoadOrCreate(GruntsPath);
            effectsSet.SetCues(Entries(effects));
            gruntPack.SetCues(Entries(grunts));
            profile.Audio.SetSounds(effectsSet, gruntPack, music);
            EditorUtility.SetDirty(effectsSet);
            EditorUtility.SetDirty(gruntPack);
            EditorUtility.SetDirty(profile.Audio);
            AssetDatabase.SaveAssets();

            Debug.Log(Report(clipCount, effects, grunts, music, unmatched), profile.Audio);
        }

        private static IEnumerable<string> ClipPaths() =>
            AssetDatabase.FindAssets("t:AudioClip", new[] { EditorPaths.AudioIncomingFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(path => path, System.StringComparer.Ordinal);

        private static IEnumerable<SoundCueClips> Entries(Dictionary<SoundCue, List<AudioClip>> clips) =>
            clips.OrderBy(pair => pair.Key).Select(pair => new SoundCueClips { Cue = pair.Key, Clips = pair.Value });

        private static SoundSetDefinition LoadOrCreate(string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<SoundSetDefinition>(path);
            if (existing != null) return existing;
            var created = ScriptableObject.CreateInstance<SoundSetDefinition>();
            AssetDatabase.CreateAsset(created, path);
            return created;
        }

        private static string Report(int clipCount, Dictionary<SoundCue, List<AudioClip>> effects, Dictionary<SoundCue, List<AudioClip>> grunts,
            AudioClip music, List<string> unmatched)
        {
            var text = new StringBuilder();
            text.AppendLine($"[XRim] Audio sets built from {clipCount} file(s) in {EditorPaths.AudioIncomingFolder}: " +
                            $"{effects.Count} effect cue(s), {grunts.Count} grunt cue(s), music {(music != null ? music.name : "none")}.");
            var missing = ((SoundCue[])System.Enum.GetValues(typeof(SoundCue)))
                .Where(cue => !effects.ContainsKey(cue) && !grunts.ContainsKey(cue))
                .Select(AudioFileNaming.FileStem).ToList();
            text.AppendLine(missing.Count == 0 ? "Every cue has a sound." : "Still silent (no file yet): " + string.Join(", ", missing));
            if (unmatched.Count > 0)
            {
                text.AppendLine($"Not used ({unmatched.Count}):");
                foreach (string path in unmatched)
                {
                    text.AppendLine("  - " + AudioCoverage.ProblemWith(path));
                }
            }

            return text.ToString();
        }
    }
}
