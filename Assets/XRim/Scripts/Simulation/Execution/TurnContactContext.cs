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

        /// <summary>
        /// Each side's planned body move this turn (the neutral move's data for no move); null when the tuning has no data for
        /// the move. The damage rules
        /// (Session 06) apply its <see cref="BodyMoveStats.DamageBonusFraction"/> (D13, TBD §5; 0 by default).
        /// </summary>
        public PerSide<BodyMoveStats> BodyMoves { get; }

        public TurnContactContext(MatchState state, RulesSettings rules, PerSide<BodyMoveStats> bodyMoves, TimelineRecorder recorder)
        {
            State = Guard.NotNull(state, nameof(state));
            Rules = Guard.NotNull(rules, nameof(rules));
            BodyMoves = Guard.NotNull(bodyMoves, nameof(bodyMoves));
            _recorder = Guard.NotNull(recorder, nameof(recorder));
        }

        /// <summary>Adds a time-stamped event to the turn's timeline (VFX, audio, replays and tests read it).</summary>
        public void Record(MatchEvent matchEvent) => _recorder.RecordEvent(matchEvent);
    }
}
