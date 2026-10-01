using System;
using XRim.Simulation.Settings;

namespace XRim.Simulation.Unity2D
{
    /// <summary>The ragdoll prefab for each segmentation (D2), so the choice can be switched live.</summary>
    public sealed class RagdollPrefabSet
    {
        private readonly Ragdoll _sixBodies;
        private readonly Ragdoll _tenBodies;

        public RagdollPrefabSet(Ragdoll sixBodies, Ragdoll tenBodies)
        {
            _sixBodies = sixBodies;
            _tenBodies = tenBodies;
        }

        public Ragdoll For(RagdollSegmentation segmentation)
        {
            Ragdoll prefab = segmentation == RagdollSegmentation.TenBodies ? _tenBodies : _sixBodies;
            if (prefab == null)
                throw new InvalidOperationException($"No {segmentation} ragdoll prefab. Run XRim/Setup/Build Placeholder Dummies.");
            return prefab;
        }
    }
}
