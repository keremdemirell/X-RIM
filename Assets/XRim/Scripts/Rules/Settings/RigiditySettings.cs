using System;
using XRim.Core.Gdd;

namespace XRim.Rules.Settings
{
    /// <summary>
    /// Bend penalty for a path (GDD §6 "Spear rigidity", TBD and conditional).
    /// Above the threshold each segment costs distance × (1 + k·Δθ); a very sharp turn breaks the path.
    /// Read by <c>RigidityInkCostModel</c>.
    /// </summary>
    [Serializable]
    public sealed class RigiditySettings
    {
        [GddTbd("§6", "Keep or cut spear rigidity", Proposal = "Keep only if it feels right")]
        public bool Enabled = false;

        /// <summary>Appendix A: 15°. Segments turning by more than this pay the bend penalty.</summary>
        public float BendThresholdDegrees = 15f;

        /// <summary>k in distance × (1 + k·Δθ), per degree of Δθ (the whole turn, as the formula is written).</summary>
        [Placeholder("§6 bend factor k: value open")]
        public float BendCostK = 0.02f;

        [Placeholder("§6 'a very sharp turn breaks the path': angle not given")]
        public float BreakAngleDegrees = 90f;

        /// <summary>
        /// Arc length over which direction changes add up for the break check, so a sharp corner that falls
        /// between two path samples still counts as one turn. About two sample spacings.
        /// </summary>
        [Placeholder("§6 does not say how a 'very sharp turn' is measured on a sampled path")]
        public float BreakWindowUnits = 20f;
    }
}
