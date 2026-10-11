using XRim.Core;
using XRim.Rules.Limbs;

namespace XRim.Simulation.Execution
{
    /// <summary>
    /// What the simulation does for one sever the rules decided (<see cref="SeverRules"/>; ARCHITECTURE §2: Rules decide, Simulation
    /// applies): the limb's joint breaks by logic (GDD §18), and when the lost arm held the item it drops. Recording the sever's events
    /// is the caller's, so they keep their place among the instant's events.
    /// </summary>
    public static class SeverApplier
    {
        public static void Apply(SeverOutcome sever, ITurnActions actions)
        {
            Guard.NotNull(sever, nameof(sever));
            Guard.NotNull(actions, nameof(actions));
            actions.SeverLimb(sever.Side, sever.Part);
            if (sever.DropsHeldItem) actions.DropHeldItem(sever.Side);
        }
    }
}
