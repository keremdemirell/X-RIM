using System;
using UnityEngine;
using UnityEngine.UIElements;
using XRim.Core;
using XRim.Core.Gdd;
using XRim.Rules.Planning;

namespace XRim.Presentation.UI
{
    /// <summary>
    /// Planning-phase HUD (UI Toolkit): countdown, weapon selector, signature buttons, Ready, remaining ink, and the
    /// opponent's public state (weapon and Ready).
    /// </summary>
    [GddTbd("§4", "Placement of weapon selector, signature buttons and Ready")]
    [GddTbd("§6", "Remaining-ink UI", Proposal = "Show ink while drawing")]
    public sealed class PlanningHud : MonoBehaviour
    {
        [SerializeField] private UIDocument _document;

        public void ShowCountdown(double secondsLeft)
        {
            // Placeholder: architecture setup only. UI comes later.
            throw new NotImplementedException("PlanningHud.ShowCountdown is not implemented yet.");
        }

        public void ShowInkRemaining(float fraction)
        {
            // Placeholder: architecture setup only. UI comes later.
            throw new NotImplementedException("PlanningHud.ShowInkRemaining is not implemented yet.");
        }

        public void ShowPublicState(Side side, PublicPlanningState state)
        {
            // Placeholder: architecture setup only. UI comes later.
            throw new NotImplementedException("PlanningHud.ShowPublicState is not implemented yet.");
        }
    }
}
