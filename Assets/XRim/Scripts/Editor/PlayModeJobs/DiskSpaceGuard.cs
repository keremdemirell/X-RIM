using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace XRim.Editor.PlayModeJobs
{
    /// <summary>
    /// Keeps Editor jobs off nearly full disks (designer, 2026-10-18, after a runaway VS Code log filled C: on 2026-10-17): a job does not
    /// start, and a running job stops, while the system drive (where Windows, Unity and VS Code keep their logs and caches) or the project
    /// drive has less than <see cref="MinimumFreeBytes"/> free.
    /// </summary>
    internal static class DiskSpaceGuard
    {
        /// <summary>10 GB (technical safety limit, not a game value).</summary>
        public const long MinimumFreeBytes = 10L * 1024L * 1024L * 1024L;

        private const double BytesPerGigabyte = 1024.0 * 1024.0 * 1024.0;

        /// <summary>Null when every watched drive has enough room; otherwise a sentence naming the drive and how much it has left.</summary>
        public static string Problem()
        {
            foreach (string root in WatchedDrives())
            {
                long free;
                try
                {
                    var drive = new DriveInfo(root);
                    if (!drive.IsReady) continue;
                    free = drive.AvailableFreeSpace;
                }
                catch (Exception exception) when (exception is ArgumentException || exception is IOException || exception is UnauthorizedAccessException)
                {
                    continue;
                }

                if (free < MinimumFreeBytes)
                    return string.Format(CultureInfo.InvariantCulture, "{0} has only {1:0.0} GB free (the safety limit is {2:0} GB)", root,
                        free / BytesPerGigabyte, MinimumFreeBytes / BytesPerGigabyte);
            }

            return null;
        }

        /// <summary>The system drive and the project's drive, each once.</summary>
        private static IEnumerable<string> WatchedDrives()
        {
            var roots = new List<string>();
            foreach (string path in new[] { Environment.SystemDirectory, Application.dataPath })
            {
                string root = Path.GetPathRoot(Path.GetFullPath(path));
                if (!string.IsNullOrEmpty(root) && !roots.Exists(r => string.Equals(r, root, StringComparison.OrdinalIgnoreCase))) roots.Add(root);
            }

            return roots;
        }
    }
}
