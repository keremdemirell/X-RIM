using System.Collections.Generic;
using UnityEngine;
using XRim.App;
using XRim.Config;
using XRim.Rules.Settings;
using XRim.Simulation;
using XRim.Simulation.Settings;
using XRim.Simulation.Unity2D;
using Object = UnityEngine.Object;

namespace XRim.Tests.PlayMode.App
{
    /// <summary>
    /// A match scene built in code for PlayMode tests: <see cref="MatchBootstrap"/> with the project's tuning, physics-only placeholder
    /// dummies built from it, and an arena root. Everything it makes goes into <c>created</c>, for the test to destroy.
    /// </summary>
    internal static class TestMatchScene
    {
        public static MatchBootstrap Create(TuningProfile tuning, List<Object> created)
        {
            var issues = new List<string>();
            RulesSettings rules = tuning.BuildRulesSettings(issues);
            SimulationSettings simulation = tuning.BuildSimulationSettings(issues);
            ArenaSpace space = tuning.ArenaSpace;
            Ragdoll six = Template(RagdollSegmentation.SixBodies, rules, simulation, space, created);
            Ragdoll ten = Template(RagdollSegmentation.TenBodies, rules, simulation, space, created);

            var arenaRoot = new GameObject("ArenaRoot");
            created.Add(arenaRoot);
            var holder = new GameObject("MatchBootstrap");
            holder.SetActive(false);
            created.Add(holder);
            var bootstrap = holder.AddComponent<MatchBootstrap>();
            bootstrap.Configure(tuning, six, ten, arenaRoot.transform, null, MatchMode.HumanVsBot, MatchStartup.WaitForCaller);
            holder.SetActive(true);
            return bootstrap;
        }

        private static Ragdoll Template(RagdollSegmentation segmentation, RulesSettings rules, SimulationSettings simulation, ArenaSpace space,
            List<Object> created)
        {
            Ragdoll ragdoll = PlaceholderRagdollBuilder.Build(new RagdollBuildSpec(simulation.Ragdoll, rules.Paths, segmentation, space,
                rules.Weapons, null));
            ragdoll.gameObject.SetActive(false);
            created.Add(ragdoll.gameObject);
            return ragdoll;
        }
    }
}
