using XRim.Config;
using XRim.Rules.Settings;
using XRim.Simulation;

namespace XRim.Presentation.ArtSpec
{
    /// <summary>
    /// What the art spec is written from (Session 13): the tuning that sizes the dummy, its held items and its effects, and where those
    /// numbers came from. A new instance holds the code's starting values; XRim/Reports/Write Art Spec fills it from the tuning assets.
    /// </summary>
    public sealed class ArtSpecInputs
    {
        /// <summary>The weapons (reach and hit width, GDD §6) and the arm's length and shoulder (the dummy's reach).</summary>
        public RulesSettings Rules = GddStartingValues.CreateRulesSettings();

        /// <summary>The ragdoll's sizes, joint limits and segmentation (D2).</summary>
        public SimulationSettings Simulation = new SimulationSettings();

        public float WorldUnitsPerArenaUnit = ArenaSpaceConfig.DefaultWorldUnitsPerArenaUnit;
        public float DebrisSizeUnits = FeelConfig.DefaultDebrisSizeUnits;
        public float FluidDropSizeUnits = FeelConfig.DefaultFluidDropSizeUnits;
        public float SparkSizeUnits = FeelConfig.DefaultSparkSizeUnits;

        /// <summary>How much height the duel camera shows, and how far the instant replay closes in: the pixel advice reads them.</summary>
        public float DuelViewHeightUnits = FeelConfig.DefaultDuelViewHeightUnits;
        public float ReplayZoomViewFraction = FeelConfig.DefaultReplayZoomViewFraction;

        /// <summary>
        /// The most height the camera ever shows: the Match scene camera's whole-arena view (XRim/Setup/Create Match Scene sets 5.75 world
        /// units of orthographic size, 1150 arena units at the default scale); the duel framing never shows more.
        /// </summary>
        public float MaxViewHeightUnits = 1150f;

        /// <summary>How much the duel framing shows below the floor, and past an arena edge (the background must cover both).</summary>
        public float FramingFloorMarginUnits = FeelConfig.DefaultFramingFloorMarginUnits;
        public float FramingEdgeMarginUnits = FeelConfig.DefaultFramingEdgeMarginUnits;

        /// <summary>Where the numbers came from, for the document's first line.</summary>
        public string Source = "the code's starting values";

        /// <summary>The day it was written, yyyy-MM-dd (passed in, so the document is the same for the same inputs).</summary>
        public string Date = string.Empty;
    }
}
