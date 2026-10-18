using System;
using System.Collections.Generic;
using XRim.Core;
using XRim.Rules;
using XRim.Rules.Events;
using XRim.Rules.Match;
using XRim.Rules.Status;
using XRim.Simulation.Recording;

namespace XRim.Simulation.Scenarios
{
    /// <summary>
    /// What a turn did, as a saved scenario remembers it: how it ended and how long it ran, every rules event in the order the turn
    /// recorded them (hits, clashes, blocks, severs, deaths, wall zaps, statuses, the moves and swings that started; not the raw physics
    /// contacts), and how each dummy came out of it. A replay is compared with it by <see cref="ScenarioComparison"/>.
    /// </summary>
    [Serializable]
    public sealed class ScenarioOutcome
    {
        public TurnEndReason EndReason;
        public long DurationMicroseconds;
        public List<ScenarioEventRecord> Events = new List<ScenarioEventRecord>();
        public ScenarioFighterOutcome Left = new ScenarioFighterOutcome();
        public ScenarioFighterOutcome Right = new ScenarioFighterOutcome();

        public ScenarioFighterOutcome Get(Side side) => side == Side.Left ? Left : Right;

        public static ScenarioOutcome Of(TurnResult turn)
        {
            Guard.NotNull(turn, nameof(turn));
            var outcome = new ScenarioOutcome
            {
                EndReason = turn.EndReason,
                DurationMicroseconds = turn.Timeline.Duration.Microseconds,
                Left = Fighter(turn.FinalBoard, Side.Left),
                Right = Fighter(turn.FinalBoard, Side.Right),
            };
            foreach (MatchEvent matchEvent in turn.Timeline.Events)
            {
                ScenarioEventRecord record = ScenarioEventRecord.Of(matchEvent);
                if (record != null) outcome.Events.Add(record);
            }

            return outcome;
        }

        private static ScenarioFighterOutcome Fighter(BoardSnapshot board, Side side)
        {
            FighterState state = board.State.Fighters[side];
            var fighter = new ScenarioFighterOutcome
            {
                Hp = state.Hp,
                IsDead = state.IsDead,
                PelvisUnits = board.Pose.Get(side).Get(BodyPart.Torso).PositionUnits,
                CurrentWeapon = state.CurrentWeapon.Value ?? string.Empty,
            };
            foreach (BodyPart part in BodyParts.All)
            {
                fighter.LimbDamage[(int)part] = state.GetLimbDamage(part);
                fighter.Severed[(int)part] = state.IsSevered(part);
            }

            foreach (IStatusEffect status in state.Statuses)
            {
                if (status.Kind == StatusKind.Stunned) fighter.IsStunned = true;
                if (status.Kind == StatusKind.Staggered) fighter.IsStaggered = true;
            }

            return fighter;
        }
    }
}
