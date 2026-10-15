using System.Collections.Generic;
using XRim.Core;
using XRim.Rules.Events;
using XRim.Rules.Match;
using XRim.Simulation.Recording;

namespace XRim.Presentation.Feel
{
    /// <summary>
    /// Plans a turn's slow motion before it plays (GDD pillar 1; presentation only). The whole turn was simulated before it is shown, so
    /// playback can start slowing down just before each big moment instead of after it: a sever (§12), a death (§11, a KO), an interrupt
    /// counter-hit (§9: a hit that cancels the other dummy's attack) and the decisive moment of a sudden-death turn (§14: its first valid
    /// hit, or a death).
    /// </summary>
    public static class SlowMotionPlanner
    {
        public static SlowMotionPlan Plan(TurnResult turn, SlowMotionSettings settings)
        {
            Guard.NotNull(turn, nameof(turn));
            Guard.NotNull(settings, nameof(settings));
            var windows = new List<SlowMotionWindow>();
            foreach (MatchEvent matchEvent in turn.Timeline.Events)
            {
                switch (matchEvent)
                {
                    case LimbSeveredEvent _:
                        Add(windows, matchEvent.Time, settings.Sever, settings.RampTurnSeconds);
                        break;
                    case FighterDiedEvent _:
                        Add(windows, matchEvent.Time, settings.KnockOut, settings.RampTurnSeconds);
                        break;
                    case AttackInterruptedEvent _:
                        Add(windows, matchEvent.Time, settings.CounterHit, settings.RampTurnSeconds);
                        break;
                }
            }

            if (TryFindSuddenDeathFinish(turn, out SimTime finish)) Add(windows, finish, settings.SuddenDeathFinish, settings.RampTurnSeconds);
            return windows.Count == 0 ? SlowMotionPlan.None : new SlowMotionPlan(windows);
        }

        /// <summary>
        /// The decisive moment of a sudden-death turn: the earliest first valid hit or death of either side (GDD §14, P4). False for a
        /// normal turn and for a sudden-death turn where nothing decisive happened.
        /// </summary>
        public static bool TryFindSuddenDeathFinish(TurnResult turn, out SimTime moment)
        {
            Guard.NotNull(turn, nameof(turn));
            moment = default;
            ExecutionReport report = turn.Report;
            if (!report.ResolvedState.IsSuddenDeath) return false;

            bool found = false;
            foreach (Side side in new[] { Side.Left, Side.Right })
            {
                Earliest(report.FirstValidHitTime[side], ref moment, ref found);
                Earliest(report.DeathTime[side], ref moment, ref found);
            }

            return found;
        }

        private static void Earliest(SimTime? candidate, ref SimTime moment, ref bool found)
        {
            if (!candidate.HasValue || found && candidate.Value >= moment) return;
            moment = candidate.Value;
            found = true;
        }

        private static void Add(List<SlowMotionWindow> windows, SimTime moment, SlowMotionMoment kind, float rampSeconds)
        {
            if (!kind.IsActive) return;
            double span = kind.TurnSeconds;
            double lead = span * XMath.Clamp01(kind.LeadFraction);
            SimTime start = moment - SimTime.FromSeconds(lead);
            SimTime end = moment + SimTime.FromSeconds(span - lead);
            windows.Add(new SlowMotionWindow(moment, start, end, kind.Speed, rampSeconds));
        }
    }
}
