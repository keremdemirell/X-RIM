using System;
using System.Collections.Generic;
using System.Text;
using XRim.Config;

namespace XRim.Presentation.Audio
{
    /// <summary>
    /// How sound files dropped into <c>Assets/XRim/Audio/Incoming/</c> are named (Session 13), so XRim → Setup → Build Audio Sets From
    /// Incoming can sort them:
    /// <list type="bullet">
    /// <item>A sound's file name starts with its cue in snake case: <c>clash_rebound.wav</c>, <c>hit_heavy_02.ogg</c>,
    /// <c>grunt_attack_3.wav</c>. Anything after the cue and an underscore, or a number straight after it, is free, so a cue can have
    /// many variants, one picked at random each time.</item>
    /// <item>Forgiving, for names typed by hand (batch 7): upper or lower case, dashes, spaces or dots between words, and capitals
    /// starting each word all work: <c>Hit Light 2.wav</c>, <c>hit-light.wav</c>, <c>HitLight.wav</c>, <c>hit_light2.wav</c>.</item>
    /// <item>Cues starting with <c>grunt_</c> go to the grunt pack; the rest to the effects set.</item>
    /// <item>Music starts with <c>music</c>: <c>music_duel_loop.ogg</c> (the first one found is the loop).</item>
    /// </list>
    /// A name that matches nothing gets a suggestion (<see cref="Suggest"/>). Pure, so it is tested without the Editor.
    /// </summary>
    public static class AudioFileNaming
    {
        public const string MusicPrefix = "music";
        private const char Separator = '_';

        /// <summary>A name this many letters long may hold one typo and still be recognised by <see cref="Suggest"/>.</summary>
        private const int LettersPerAllowedTypo = 4;

        /// <summary>A first word shorter than this is too vague to suggest sounds from.</summary>
        private const int MinSuggestedWordLength = 3;

        private const int MaxSuggestions = 5;

        private static readonly SoundCue[] AllCues = (SoundCue[])Enum.GetValues(typeof(SoundCue));

        /// <summary>A cue's name in files: <c>GruntBigHurt</c> → <c>grunt_big_hurt</c>.</summary>
        public static string FileStem(SoundCue cue)
        {
            string name = cue.ToString();
            var stem = new StringBuilder(name.Length + 4);
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                if (char.IsUpper(c) && i > 0) stem.Append(Separator);
                stem.Append(char.ToLowerInvariant(c));
            }

            return stem.ToString();
        }

        /// <summary>
        /// The cue a file is for: its name (without folder or extension, read forgivingly) is the cue's stem, or starts with it followed by
        /// an underscore or a number. The longest matching stem wins, so <c>hit_heavy_1</c> is a heavy hit, not a head hit. False for
        /// music and unknown names.
        /// </summary>
        public static bool TryParse(string fileName, out SoundCue cue)
        {
            cue = default;
            string name = NormalizedName(fileName);
            if (name.Length == 0 || IsMusicName(name)) return false;

            int best = -1;
            foreach (SoundCue candidate in AllCues)
            {
                string stem = FileStem(candidate);
                if (!StartsWithWord(name, stem) || stem.Length <= best) continue;
                best = stem.Length;
                cue = candidate;
            }

            return best >= 0;
        }

        /// <summary>True for a music file: its name is <c>music</c>, or starts with <c>music</c> and an underscore or a number.</summary>
        public static bool IsMusic(string fileName) => IsMusicName(NormalizedName(fileName));

        /// <summary>
        /// For a file name that is neither a sound nor music, what it was probably meant to be: the name with a typo fixed
        /// (<c>hit_ligth_2</c> → <c>hit_light</c>, <c>musik</c> → <c>music</c>), or else the sounds its first word belongs to
        /// (<c>clash.wav</c> → <c>clash_rebound, clash_crush or clash_slide</c>). Null when nothing is close (and for a name that works).
        /// </summary>
        public static string Suggest(string fileName)
        {
            string name = NormalizedName(fileName);
            if (name.Length == 0 || IsMusicName(name) || TryParse(fileName, out _)) return null;

            string typo = ClosestName(name);
            if (typo != null) return typo;

            int end = name.IndexOf(Separator);
            string word = end >= 0 ? name.Substring(0, end) : name;
            if (word.Length < MinSuggestedWordLength) return null;
            var matches = new List<string>();
            foreach (SoundCue cue in AllCues)
            {
                string stem = FileStem(cue);
                if (Array.IndexOf(stem.Split(Separator), word) >= 0) matches.Add(stem);
            }

            if (matches.Count == 0 || matches.Count > MaxSuggestions) return null;
            if (matches.Count == 1) return matches[0];
            return string.Join(", ", matches.GetRange(0, matches.Count - 1)) + " or " + matches[matches.Count - 1];
        }

        /// <summary>The cue stem or <c>music</c> closest to the start of the name, within a typo per few letters; null when none is.</summary>
        private static string ClosestName(string name)
        {
            string best = null;
            int bestDistance = int.MaxValue;
            var candidates = new List<string> { MusicPrefix };
            foreach (SoundCue cue in AllCues)
            {
                candidates.Add(FileStem(cue));
            }

            foreach (string candidate in candidates)
            {
                int allowed = Math.Max(1, candidate.Length / LettersPerAllowedTypo);
                int longest = Math.Min(name.Length, candidate.Length + allowed);
                for (int length = Math.Max(1, candidate.Length - allowed); length <= longest; length++)
                {
                    if (length < name.Length && !IsWordEnd(name[length])) continue; // compare whole words only
                    int distance = Distance(name.Substring(0, length), candidate);
                    if (distance > allowed || distance > bestDistance) continue;
                    if (distance == bestDistance && best != null && candidate.Length <= best.Length) continue;
                    best = candidate;
                    bestDistance = distance;
                }
            }

            return best;
        }

        /// <summary>How many letters must be added, removed, changed or swapped with a neighbour to turn one text into the other.</summary>
        private static int Distance(string a, string b)
        {
            var d = new int[a.Length + 1, b.Length + 1];
            for (int i = 0; i <= a.Length; i++) d[i, 0] = i;
            for (int j = 0; j <= b.Length; j++) d[0, j] = j;
            for (int i = 1; i <= a.Length; i++)
            {
                for (int j = 1; j <= b.Length; j++)
                {
                    int change = a[i - 1] == b[j - 1] ? 0 : 1;
                    int best = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + change);
                    if (i > 1 && j > 1 && a[i - 1] == b[j - 2] && a[i - 2] == b[j - 1]) best = Math.Min(best, d[i - 2, j - 2] + 1);
                    d[i, j] = best;
                }
            }

            return d[a.Length, b.Length];
        }

        private static bool IsMusicName(string name) => StartsWithWord(name, MusicPrefix);

        /// <summary>The name is the word, or starts with it followed by an underscore or a number.</summary>
        private static bool StartsWithWord(string name, string word) =>
            name == word || (name.Length > word.Length && name.StartsWith(word, StringComparison.Ordinal) && IsWordEnd(name[word.Length]));

        private static bool IsWordEnd(char next) => next == Separator || char.IsDigit(next);

        /// <summary>
        /// The file's name as the matching reads it: without folder or extension, lower case, words joined by single underscores (spaces,
        /// dashes and dots between words, and a capital starting a word after a small letter, all count as a break).
        /// </summary>
        private static string NormalizedName(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return string.Empty;
            string name = fileName.Replace('\\', '/');
            int slash = name.LastIndexOf('/');
            if (slash >= 0) name = name.Substring(slash + 1);
            int dot = name.LastIndexOf('.');
            if (dot > 0) name = name.Substring(0, dot);
            name = name.Trim();

            var normal = new StringBuilder(name.Length + 4);
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                bool wordBreak = c == Separator || c == ' ' || c == '-' || c == '.';
                bool capitalStartsWord = char.IsUpper(c) && i > 0 && char.IsLower(name[i - 1]);
                if ((wordBreak || capitalStartsWord) && normal.Length > 0 && normal[normal.Length - 1] != Separator) normal.Append(Separator);
                if (!wordBreak) normal.Append(char.ToLowerInvariant(c));
            }

            if (normal.Length > 0 && normal[normal.Length - 1] == Separator) normal.Length--;
            return normal.ToString();
        }
    }
}
