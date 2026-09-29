using System;
using XRim.Core.Gdd;

namespace XRim.Rules.Settings
{
    /// <summary>
    /// Bend penalty for a path (GDD §6 "Spear rigidity", TBD and conditional).
    /// Above the threshold each segment costs distance × (1 + k·Δθ); a very sharp turn breaks the path.
    /// </summary>
    [Serializable]
    public sealed class RigiditySettings
    {
        [GddTbd("§6", "Keep or cut spear rigidity", Proposal = "Keep only if it feels right")]
        public bool Enabled = false;

        /// <summary>Appendix A: 15°.</summary>
        public float BendThresholdDegrees = 15f;

        [Placeholder("§6 bend factor k: value open")]
        public float BendCostK = 0.02f;

        [Placeholder("§6 'a very sharp turn breaks the path': angle not given")]
        public float BreakAngleDegrees = 90f;
    }
}
