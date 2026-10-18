using System;
using XRim.Core;
using XRim.Simulation.Execution;
using XRim.Simulation.Recording;

namespace XRim.Editor.Scenarios
{
    /// <summary>
    /// How to build one seed regression scenario (Session 16) and what its turn must show before it is saved: the moment it is named for,
    /// so a fixture never records a turn that missed (a moved guard, a changed weapon) as the "expected" outcome.
    /// </summary>
    internal sealed class SeedScenario
    {
        private readonly Func<Func<TurnInput, TurnResult>, TurnInput> _build;
        private readonly Func<TurnInput, TurnResult, string> _check;

        /// <param name="build">Makes the turn to save; it is handed a simulator for any setup turn it needs first.</param>
        /// <param name="check">Null when the resolved turn shows the moment; otherwise why not, in a sentence.</param>
        public SeedScenario(string fileName, string title, string note, Func<Func<TurnInput, TurnResult>, TurnInput> build,
            Func<TurnInput, TurnResult, string> check)
        {
            FileName = fileName;
            Title = title;
            Note = note;
            _build = Guard.NotNull(build, nameof(build));
            _check = Guard.NotNull(check, nameof(check));
        }

        /// <summary>The fixture's file name without the extension ("seed-…").</summary>
        public string FileName { get; }

        public string Title { get; }

        public string Note { get; }

        public TurnInput Build(Func<TurnInput, TurnResult> simulate) => _build(simulate);

        public string Check(TurnInput input, TurnResult result) => _check(input, result);
    }
}
