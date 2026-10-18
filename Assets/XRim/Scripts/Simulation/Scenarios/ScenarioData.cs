using System;
using XRim.Rules.Settings;
using XRim.Simulation.Physics;

namespace XRim.Simulation.Scenarios
{
    /// <summary>
    /// A <see cref="Scenario"/> as it is written to a file: plain public fields only (no properties, no nulls, no interfaces), so Unity's
    /// JSON serializer and any other one read and write it. The pose and the settings are already serializable and are stored as they
    /// are; the rules state and the plans have their own data shapes. <see cref="ScenarioCodec"/> converts both ways.
    /// </summary>
    [Serializable]
    public sealed class ScenarioData
    {
        /// <summary>The newest layout this code writes and reads. A file with a higher number was written by newer code.</summary>
        public const int CurrentFormatVersion = 1;

        public int FormatVersion = CurrentFormatVersion;
        public string Name = string.Empty;
        public string Note = string.Empty;

        /// <summary>When it was saved (ISO 8601, local time), for people reading the file; never read back.</summary>
        public string SavedAt = string.Empty;

        public float WorldUnitsPerArenaUnit;
        public MatchStateData State = new MatchStateData();
        public PoseSnapshot Pose = new PoseSnapshot();
        public PlanData LeftPlan = new PlanData();
        public PlanData RightPlan = new PlanData();
        public RulesSettings Rules = new RulesSettings();
        public SimulationSettings Simulation = new SimulationSettings();

        /// <summary>A serializer cannot store "no outcome" as null, so this flag says whether <see cref="Expected"/> holds one.</summary>
        public bool HasExpected;

        public ScenarioOutcome Expected = new ScenarioOutcome();
    }
}
