using System;
using XRim.Rules.Settings;

namespace XRim.Rules.Planning
{
    /// <summary>
    /// Authority-side check of a locked plan: loadout, ink budget, path validity (GDD §18 "the server
    /// should check every input"). Timing checks happen in <see cref="PlanningSession"/> as commands arrive.
    /// </summary>
    public sealed class PlanValidator
    {
        public CommandResult Validate(TurnPlan plan, PlanningConstraints constraints, RulesSettings settings)
        {
            // Placeholder: architecture setup only. Gameplay implementation comes later.
            throw new NotImplementedException("PlanValidator.Validate is not implemented yet.");
        }
    }
}
