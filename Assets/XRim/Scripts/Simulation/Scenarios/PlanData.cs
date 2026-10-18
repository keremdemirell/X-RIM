using System;
using System.Collections.Generic;
using XRim.Core;
using XRim.Rules;

namespace XRim.Simulation.Scenarios
{
    /// <summary>
    /// One player's locked plan (<see cref="XRim.Rules.Planning.TurnPlan"/>) in plain fields: the weapon, the body move, the path the
    /// rules built (torso frame, arena units; positions only, never timestamps, §4), a signature move and Ready.
    /// </summary>
    [Serializable]
    public sealed class PlanData
    {
        public string Weapon = string.Empty;
        public BodyMove BodyMove;
        public List<Vec2> Path = new List<Vec2>();

        /// <summary>The signature move's id; empty when the plan has none.</summary>
        public string Signature = string.Empty;

        public bool PressedReady;
    }
}
