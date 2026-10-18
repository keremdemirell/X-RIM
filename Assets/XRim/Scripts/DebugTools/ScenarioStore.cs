using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using XRim.Config;
using XRim.Simulation.Scenarios;

namespace XRim.DebugTools
{
    /// <summary>
    /// Saves and loads scenarios (Session 16): a frozen board, both players' plans, the settings snapshot and what the turn did. Because a
    /// turn is a pure function of these, a saved scenario replays exactly and can become a regression test or a feel-tuning fixture.
    /// Files are JSON (<see cref="ScenarioJson"/>) in one folder: in the Editor <c>Logs/Scenarios/</c> under the project (git ignores it, so
    /// experiments never clutter the history; "Add to regression tests" copies one into the tests), in a development build the app's
    /// data folder.
    /// </summary>
    public sealed class ScenarioStore
    {
        public const string FileExtension = ".json";

        /// <summary>The PlayMode regression tests replay every scenario in this project folder (Session 16); its files are committed.</summary>
        public const string RegressionFolderInProject = "Assets/XRim/Tests/PlayMode/Scenarios";

        private const string FolderName = "Scenarios";
        private const string EditorParentFolderName = "Logs";
        private const int MaxFileNameLength = 80;
        private const char FileNameSeparator = '-';

        public ScenarioStore(string folder)
        {
            if (string.IsNullOrEmpty(folder)) throw new ArgumentException("A scenario folder is needed.", nameof(folder));
            Folder = folder;
        }

        public string Folder { get; }

        /// <summary>The store in the default folder (see the class summary).</summary>
        public static ScenarioStore CreateDefault() => new ScenarioStore(DefaultFolder());

        public static string DefaultFolder()
        {
#if UNITY_EDITOR
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", EditorParentFolderName, FolderName));
#else
            return Path.Combine(Application.persistentDataPath, FolderName);
#endif
        }

        /// <summary>Saves the scenario under a file name made from its name (never overwriting another file) and returns the path.</summary>
        public string Save(Scenario scenario)
        {
            string path = UniquePath(FileNameFor(scenario.Name));
            Save(path, scenario);
            return path;
        }

        public void Save(string path, Scenario scenario)
        {
            string folder = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);
            File.WriteAllText(path, ScenarioJson.Write(scenario, DateTime.Now), new UTF8Encoding(false));
        }

        /// <exception cref="FormatException">The file is not a scenario.</exception>
        /// <exception cref="NotSupportedException">It was written by newer code.</exception>
        public Scenario Load(string path) => ScenarioJson.Read(File.ReadAllText(path));

        /// <summary>Every scenario file in the folder, the newest first; none when the folder does not exist yet.</summary>
        public IReadOnlyList<string> List()
        {
            if (!Directory.Exists(Folder)) return Array.Empty<string>();
            return Directory.GetFiles(Folder, "*" + FileExtension).OrderByDescending(File.GetLastWriteTimeUtc).ToList();
        }

        /// <summary>The regression tests' folder on disk (meaningful in the Editor, where the project's folders exist).</summary>
        public static string RegressionFolder() => Path.GetFullPath(Path.Combine(Application.dataPath, "..", RegressionFolderInProject));

        /// <summary>
        /// Copies a saved scenario into the regression tests' folder and returns its new path. Refuses one without a recorded outcome (a
        /// test would have nothing to compare with) and one that is already there.
        /// </summary>
        /// <exception cref="InvalidOperationException">The scenario has no recorded outcome.</exception>
        /// <exception cref="IOException">A regression test of that name exists.</exception>
        public string CopyToRegressionTests(string path)
        {
            string fileName = Path.GetFileName(path);
            if (Load(path).Expected == null)
                throw new InvalidOperationException($"{fileName} has no recorded outcome, so a regression test would have nothing to compare with.");

            string folder = RegressionFolder();
            string target = Path.Combine(folder, fileName);
            if (File.Exists(target)) throw new IOException($"{fileName} is already a regression test.");
            Directory.CreateDirectory(folder);
            File.Copy(path, target);
            return target;
        }

        /// <summary>A file name made from a scenario name: letters and digits kept, everything else a dash, at most 80 characters.</summary>
        public static string FileNameFor(string name)
        {
            var text = new StringBuilder();
            foreach (char letter in name ?? string.Empty)
            {
                bool keep = char.IsLetterOrDigit(letter) || letter == '_';
                if (keep) text.Append(char.ToLowerInvariant(letter));
                else if (text.Length > 0 && text[text.Length - 1] != FileNameSeparator) text.Append(FileNameSeparator);
                if (text.Length >= MaxFileNameLength) break;
            }

            string trimmed = text.ToString().Trim(FileNameSeparator);
            return (trimmed.Length > 0 ? trimmed : Scenario.DefaultName) + FileExtension;
        }

        private string UniquePath(string fileName)
        {
            string path = Path.Combine(Folder, fileName);
            string stem = Path.GetFileNameWithoutExtension(fileName);
            for (int copy = 2; File.Exists(path); copy++)
            {
                path = Path.Combine(Folder, stem + FileNameSeparator + copy + FileExtension);
            }

            return path;
        }
    }
}
