using System;
using XRim.Core;
using XRim.Rules;
using XRim.Simulation.Recording;
using XRim.Simulation.Physics;

namespace XRim.Simulation.Execution
{
    /// <summary>
    /// The execution loop. Simulation detects, Rules decide, Simulation applies:
    /// load the frozen board; each fixed step, drivers set targets, the world steps, new contacts are sorted by
    /// time-to-impact (then a stable id) and handed to the rules (hit, clash, block, wall); outcomes are applied
    /// (interrupt, knock off path, sever by logic) and recorded. Stops when both paths are done and physics has
    /// settled, or at the execution hard cap (GDD §3, §9).
    /// </summary>
    public sealed class TurnSimulator : ITurnSimulator
    {
        private IPhysicsWorld World { get; }
        private RulePolicies Policies { get; }

        public TurnSimulator(IPhysicsWorld world, RulePolicies policies)
        {
            World = Guard.NotNull(world, nameof(world));
            Policies = Guard.NotNull(policies, nameof(policies));
        }

        public TurnResult Simulate(TurnInput input)
        {
            // Placeholder: architecture setup only. Gameplay implementation comes later.
            throw new NotImplementedException("TurnSimulator.Simulate is not implemented yet.");
        }
    }
}
