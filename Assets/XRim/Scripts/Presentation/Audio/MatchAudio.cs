using System;
using UnityEngine;
using XRim.Presentation.Playback;
using XRim.Rules.Events;

namespace XRim.Presentation.Audio
{
    /// <summary>
    /// Metal clashes, impacts, joint snaps, bolt scatter, wall zaps and comic effort grunts (GDD §2, Decided).
    /// Grunt packs are cosmetics (§16): they change sounds only.
    /// </summary>
    public sealed class MatchAudio : MonoBehaviour, IMatchEventReactor
    {
        public void OnMatchEvent(MatchEvent matchEvent)
        {
            // Placeholder: architecture setup only. Audio comes later.
            throw new NotImplementedException("MatchAudio.OnMatchEvent is not implemented yet.");
        }
    }
}
