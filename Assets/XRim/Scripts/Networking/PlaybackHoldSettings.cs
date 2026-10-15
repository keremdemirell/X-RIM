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
        /// <see cref="PlaybackHoldMode.TimelineDuration"/>: seconds added to the recorded duration, because hitstops, slow motion and the
        /// camera settling stretch playback. Session 13 worked it out from the Feel asset's values: a turn with a KO stretches about
        /// 1.4 s (its slow motion and freeze), one that also severs a limb and lands a counter-hit at other moments up to about 2.9 s,
        /// plus 0.35 s of the camera settling; the bot batch reports the worst stretch it met. The Match scene waits for the client's
        /// report instead.
        /// </summary>
        [Placeholder("Session 13: worked out from the feel values (a KO, a sever and a counter-hit in one turn); re-measure at PT5")]
        public float ExtraSeconds = 3.5f;

        /// <summary><see cref="PlaybackHoldMode.ClientReport"/>: how long to wait for the client's report before carrying on.</summary>
        [Placeholder("No client exists yet to measure playback time")]
        public float ClientReportTimeoutSeconds = 10f;
    }
}
