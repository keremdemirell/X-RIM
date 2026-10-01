using System;
using XRim.Core.Gdd;

namespace XRim.Networking
{
    /// <summary>When the next planning timer starts after a turn resolves. Not a GDD value; it never changes an outcome.</summary>
    [Serializable]
    public sealed class PlaybackHoldSettings
    {
        public PlaybackHoldMode Mode = PlaybackHoldMode.None;

        /// <summary>
        /// <see cref="PlaybackHoldMode.TimelineDuration"/>: seconds added to the recorded duration, because hitstop and
        /// slow motion stretch playback (Session 13).
        /// </summary>
        [Placeholder("Playback length with feel effects is not known until Session 13")]
        public float ExtraSeconds = 1f;

        /// <summary><see cref="PlaybackHoldMode.ClientReport"/>: how long to wait for the client's report before carrying on.</summary>
        [Placeholder("No client exists yet to measure playback time")]
        public float ClientReportTimeoutSeconds = 10f;
    }
}
