using System;
using UnityEngine;
using XRim.Presentation.Playback;
using XRim.Rules.Events;

namespace XRim.Presentation.Vfx
{
    /// <summary>
    /// Bloodless mechanical destruction (GDD §2, Decided): bolts, nuts, springs, sparks and hydraulic fluid.
    /// Debris is cosmetic physics in the visual scene and never feeds back into the simulation.
    /// </summary>
    public sealed class MatchVfx : MonoBehaviour, IMatchEventReactor
    {
        public void OnMatchEvent(MatchEvent matchEvent)
        {
            // Placeholder: architecture setup only. VFX come later.
            throw new NotImplementedException("MatchVfx.OnMatchEvent is not implemented yet.");
        }
    }
}
