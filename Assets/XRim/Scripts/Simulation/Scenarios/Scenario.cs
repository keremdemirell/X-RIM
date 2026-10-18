using XRim.Core;
using XRim.Simulation.Execution;

namespace XRim.Simulation.Scenarios
{
    /// <summary>
    /// A saved turn (ARCHITECTURE §1: a turn is a pure function of its board, both plans and the settings): everything needed to resolve
    /// it again, so it replays as a regression test, a feel fixture or a moment to study. <see cref="Expected"/> is what the turn did
    /// when it was saved (null when nothing was recorded), so a replay can be compared with it (<see cref="ScenarioComparison"/>).
    /// Saved and loaded through <see cref="ScenarioCodec"/>.
    /// </summary>
    public sealed class Scenario
    {
        /// <summary>The name a scenario gets when none is given.</summary>
        public const string DefaultName = "scenario";

        public string Name { get; }

        /// <summary>Free text: where the scenario came from and what it shows.</summary>
        public string Note { get; }

        /// <summary>The frozen board, both plans and the rules and simulation settings: everything the turn depends on.</summary>
        public TurnInput Input { get; }

        /// <summary>
        /// The physics scale the turn was simulated at (world units per arena unit, the profile's arena space), so a replay builds the
        /// same physics world.
        /// </summary>
        public float WorldUnitsPerArenaUnit { get; }

        /// <summary>What the turn did when it was saved; null when no outcome was recorded.</summary>
        public ScenarioOutcome Expected { get; }

        public Scenario(string name, string note, TurnInput input, float worldUnitsPerArenaUnit, ScenarioOutcome expected = null)
        {
            Name = string.IsNullOrEmpty(name) ? DefaultName : name;
            Note = note ?? string.Empty;
            Input = Guard.NotNull(input, nameof(input));
            WorldUnitsPerArenaUnit = Guard.Positive(worldUnitsPerArenaUnit, nameof(worldUnitsPerArenaUnit));
            Expected = expected;
        }

        /// <summary>This scenario with another recorded outcome (null: none).</summary>
        public Scenario WithExpected(ScenarioOutcome expected) => new Scenario(Name, Note, Input, WorldUnitsPerArenaUnit, expected);
    }
}
