using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using XRim.Config;
using XRim.Core;

namespace XRim.Presentation.Audio
{
    /// <summary>
    /// The words of XRim → Reports → Audio Coverage (Session 13): which sounds the game plays and how many versions each has, which are
    /// still silent (the most important first), the music, every file in the Incoming folder the game cannot use and why (a name it does
    /// not know, with a suggestion; a format Unity cannot read; a second music file), and files added since the sound sets were last
    /// built. The Editor gathers the facts; this writes them for a person. Pure.
    /// </summary>
    public static class AudioCoverage
    {
        /// <summary>Every cue once, the ones a duel needs most first: the order to find sounds in (Docs/SOUND_GUIDE.md follows it).</summary>
        public static readonly IReadOnlyList<SoundCue> Priority = new[]
        {
            SoundCue.HitLight, SoundCue.HitHeavy, SoundCue.ClashRebound, SoundCue.GruntHurt, SoundCue.JointSnap, SoundCue.BoltScatter,
            SoundCue.SwingWhoosh, SoundCue.GruntAttack, SoundCue.HitHead, SoundCue.ClashCrush, SoundCue.BlockFull, SoundCue.TorsoSplit,
            SoundCue.GruntKnockOut, SoundCue.GruntBigHurt, SoundCue.WallZap, SoundCue.ClashSlide, SoundCue.BlockPartial, SoundCue.WeaponDrop,
            SoundCue.WallWarning, SoundCue.BodyMove, SoundCue.GruntEffort,
        };

        private const string Folder = "Assets/XRim/Audio/Incoming/";
        private const string BuildMenu = "XRim → Setup → Build Audio Sets From Incoming";
        private const string Guide = "Docs/SOUND_GUIDE.md";

        /// <summary>The sound formats Unity imports (lower case, with the dot).</summary>
        private static readonly string[] ReadableExtensions = { ".wav", ".ogg", ".mp3", ".aif", ".aiff" };

        /// <summary>Files that belong in the folder without being sounds (Unity's own notes, read-me files).</summary>
        private static readonly string[] IgnoredExtensions = { ".meta", ".md", ".txt" };

        /// <param name="versionsOf">How many sound files each cue has in the sound sets now.</param>
        /// <param name="musicName">The music loop in the Audio asset now; null or empty when there is none.</param>
        /// <param name="folderFiles">Every file in the Incoming folder (names or paths).</param>
        /// <param name="builtNames">The names, without extension, of every file the sound sets and the music use now.</param>
        public static string Report(Func<SoundCue, int> versionsOf, string musicName, IReadOnlyList<string> folderFiles, IEnumerable<string> builtNames)
        {
            Guard.NotNull(versionsOf, nameof(versionsOf));
            Guard.NotNull(folderFiles, nameof(folderFiles));
            Guard.NotNull(builtNames, nameof(builtNames));
            var files = new List<string>(folderFiles);
            files.Sort((a, b) => string.CompareOrdinal(Path.GetFileName(a), Path.GetFileName(b)));
            var built = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string name in builtNames)
            {
                if (!string.IsNullOrEmpty(name)) built.Add(name);
            }

            var text = new StringBuilder();
            text.AppendLine($"Audio coverage: {files.FindAll(IsSoundFile).Count} sound file(s) in {Folder}");
            WriteCues(text, versionsOf);
            text.AppendLine(string.IsNullOrEmpty(musicName) ? "Music: none yet (a file whose name starts with \"music\")." : $"Music: {musicName}.");

            var problems = new List<string>();
            var waiting = new List<string>();
            string firstMusic = null;
            foreach (string file in files)
            {
                string problem = ProblemWith(file);
                if (problem != null)
                {
                    problems.Add(problem);
                    continue;
                }

                if (!IsSoundFile(file)) continue;
                if (AudioFileNaming.IsMusic(file))
                {
                    if (firstMusic != null)
                    {
                        problems.Add($"{Path.GetFileName(file)}: only one music loop is used ({firstMusic}, the first by name); this one is ignored.");
                        continue;
                    }

                    firstMusic = Path.GetFileName(file);
                }

                if (!built.Contains(Path.GetFileNameWithoutExtension(file))) waiting.Add(Path.GetFileName(file));
            }

            if (problems.Count > 0)
            {
                text.AppendLine($"Files the game cannot use ({problems.Count}):");
                foreach (string problem in problems)
                {
                    text.AppendLine("  - " + problem);
                }
            }

            if (waiting.Count > 0)
            {
                text.AppendLine($"New since the sound sets were last built ({waiting.Count}): {string.Join(", ", waiting)}. " +
                                $"Run {BuildMenu} to add them.");
            }

            if (problems.Count == 0 && waiting.Count == 0) text.AppendLine("Every sound file in the folder is in the game.");
            return text.ToString().TrimEnd();
        }

        /// <summary>Why a file in the Incoming folder cannot be used, in plain words with the fix; null when it can (or is not a sound).</summary>
        public static string ProblemWith(string fileName)
        {
            Guard.NotNull(fileName, nameof(fileName));
            string file = Path.GetFileName(fileName);
            string extension = Path.GetExtension(fileName).ToLowerInvariant();
            if (Array.IndexOf(IgnoredExtensions, extension) >= 0) return null;
            if (extension.Length == 0) return $"{file}: has no type at the end of its name. Sound files end in .wav or .ogg.";
            if (Array.IndexOf(ReadableExtensions, extension) < 0)
                return $"{file}: Unity cannot read {extension} files. Convert it to .wav or .ogg (see \"Converting a file\" in {Guide}).";
            if (AudioFileNaming.IsMusic(fileName) || AudioFileNaming.TryParse(fileName, out _)) return null;

            string suggestion = AudioFileNaming.Suggest(fileName);
            return suggestion == null
                ? $"{file}: not a sound name the game knows. Rename it to start with one of the names in {Guide}."
                : $"{file}: not a sound name the game knows. Did you mean {suggestion}? Rename the file to start with that name.";
        }

        private static void WriteCues(StringBuilder text, Func<SoundCue, int> versionsOf)
        {
            var playing = new List<string>();
            var silent = new List<string>();
            foreach (SoundCue cue in Priority)
            {
                int versions = versionsOf(cue);
                string stem = AudioFileNaming.FileStem(cue);
                if (versions <= 0) silent.Add(stem);
                else playing.Add(versions == 1 ? stem : $"{stem} ({versions} versions)");
            }

            text.AppendLine($"Sounds that play: {playing.Count} of {Priority.Count}" + (playing.Count > 0 ? ": " + string.Join(", ", playing) + "." : "."));
            text.AppendLine(silent.Count == 0
                ? "Still silent: none. Every sound has at least one file."
                : "Still silent, the most important first: " + string.Join(", ", silent) + ".");
        }

        private static bool IsSoundFile(string fileName) => Array.IndexOf(ReadableExtensions, Path.GetExtension(fileName).ToLowerInvariant()) >= 0;
    }
}
