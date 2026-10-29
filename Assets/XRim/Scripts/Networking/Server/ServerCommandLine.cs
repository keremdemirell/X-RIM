using System.Collections.Generic;
using System.Globalization;
using XRim.Core;

namespace XRim.Networking.Server
{
    /// <summary>
    /// The dedicated server's command-line options (Session 19): <c>-port N</c> picks the TCP port (1–65535; 0 lets the system pick a free
    /// one, for tests). Anything else on the line (Unity's own options such as <c>-batchmode</c> or <c>-logFile</c>) is left alone.
    /// </summary>
    public static class ServerCommandLine
    {
        public const string PortOption = "-port";

        private const int MaxPort = 65535;

        /// <summary>The port the line asks for, or <paramref name="fallback"/> when it names none or a number that is not a port.</summary>
        public static int Port(IReadOnlyList<string> arguments, int fallback)
        {
            Guard.NotNull(arguments, nameof(arguments));
            for (int i = 0; i < arguments.Count - 1; i++)
            {
                if (!string.Equals(arguments[i], PortOption, System.StringComparison.OrdinalIgnoreCase)) continue;
                if (int.TryParse(arguments[i + 1], NumberStyles.None, CultureInfo.InvariantCulture, out int port) && port <= MaxPort) return port;
            }

            return fallback;
        }
    }
}
