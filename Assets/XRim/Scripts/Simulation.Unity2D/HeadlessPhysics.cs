using System;
using UnityEngine;
using XRim.Config;
using XRim.Core;
using XRim.Rules;
using XRim.Rules.Settings;
using XRim.Simulation.Execution;
using XRim.Simulation.Recording;
using XRim.Simulation.Settings;
using Object = UnityEngine.Object;

namespace XRim.Simulation.Unity2D
{
    /// <summary>
    /// A hidden physics world of its own for one settings snapshot (Session 16): a physics-only placeholder dummy (no renderers) built from
    /// those settings and a <see cref="Unity2DPhysicsWorld"/> around it, so a saved scenario, a regression test or a balance batch resolves
    /// turns on exactly the dummies its settings describe, whatever the scene holds. Needs Play mode, like the world. While it lives, 2D
    /// physics only moves when stepped; disposing it puts the previous simulation mode back.
    /// </summary>
    public sealed class HeadlessPhysics : IDisposable
    {
        private readonly Ragdoll _template;
        private readonly SimulationMode2D _previousMode;
        private bool _disposed;

        public HeadlessPhysics(RulesSettings rules, SimulationSettings simulation, ArenaSpace space)
        {
            Guard.NotNull(rules, nameof(rules));
            Guard.NotNull(simulation, nameof(simulation));
            _previousMode = Physics2D.simulationMode;
            Physics2D.simulationMode = SimulationMode2D.Script;

            Segmentation = simulation.Segmentation;
            _template = PlaceholderRagdollBuilder.Build(new RagdollBuildSpec(simulation.Ragdoll, rules.Paths, Segmentation, space, rules.Weapons, null));
            _template.gameObject.SetActive(false);
            bool tenBodies = Segmentation == RagdollSegmentation.TenBodies;
            World = new Unity2DPhysicsWorld(space, new RagdollPrefabSet(tenBodies ? null : _template, tenBodies ? _template : null));
        }

        public Unity2DPhysicsWorld World { get; }

        /// <summary>The one segmentation (D2) this world has a dummy for: the settings snapshot's.</summary>
        public RagdollSegmentation Segmentation { get; }

        /// <summary>A turn simulator on this world with the game's own rules (default policies, the hit, clash and block handler).</summary>
        public TurnSimulator CreateSimulator() => new TurnSimulator(World, new RulePolicies());

        /// <summary>Resolves one turn on a dummy built from the turn's own settings, then lets the world go.</summary>
        public static TurnResult SimulateOnce(TurnInput input, ArenaSpace space)
        {
            Guard.NotNull(input, nameof(input));
            using (var physics = new HeadlessPhysics(input.Rules, input.Simulation, space))
            {
                return physics.CreateSimulator().Simulate(input);
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            World.Dispose();
            if (_template != null)
            {
                if (Application.isPlaying) Object.Destroy(_template.gameObject);
                else Object.DestroyImmediate(_template.gameObject);
            }

            Physics2D.simulationMode = _previousMode;
        }
    }
}
