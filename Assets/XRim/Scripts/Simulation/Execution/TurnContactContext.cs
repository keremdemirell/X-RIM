using XRim.Core;
using XRim.Rules.Events;
using XRim.Rules.Match;
using XRim.Rules.Settings;
using XRim.Simulation.Recording;

namespace XRim.Simulation.Execution
{
    /// <summary>
    /// What an <see cref="ITurnContactHandler"/> can read and change while a turn runs. Sessions 06 and 07 extend it with
    /// what their rules apply (interrupting an attack, knockback, severing).
    /// </summary>
    public sealed class TurnContactContext
    {
        private readonly TimelineRecorder _recorder;

        /// <summary>The rules state being resolved this turn. Its final value is the turn's resolved state.</summary>
        public MatchState State { get; }

        public RulesSettings Rules { get; }

        public TurnContactContext(MatchState state, RulesSettings rules, TimelineRecorder recorder)
        {
            State = Guard.NotNull(state, nameof(state));
            Rules = Guard.NotNull(rules, nameof(rules));
            _recorder = Guard.NotNull(recorder, nameof(recorder));
        }

        /// <summary>Adds a time-stamped event to the turn's timeline (VFX, audio, replays and tests read it).</summary>
        public void Record(MatchEvent matchEvent) => _recorder.RecordEvent(matchEvent);
    }
}
