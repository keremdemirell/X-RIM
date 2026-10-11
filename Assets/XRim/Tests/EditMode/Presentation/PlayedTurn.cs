using XRim.Core;
using XRim.Rules;
using XRim.Rules.Combat;
using XRim.Rules.Damage;
using XRim.Rules.Events;
using XRim.Rules.Match;
using XRim.Rules.Settings;
using XRim.Simulation.Physics;
using XRim.Simulation.Recording;

namespace XRim.Tests.EditMode.Presentation
{
    /// <summary>Builds a played turn with landed hits, as the simulator reports it.</summary>
    internal static class PlayedTurn
    {
        public const double Seconds = 1.0;

        /// <summary>A torso hit by a rapier on <paramref name="victim"/>, taking <paramref name="hpDamage"/> HP.</summary>
        public static HitLandedEvent Hit(Side victim, double atSeconds, float hpDamage)
        {
            SimTime time = SimTime.FromSeconds(atSeconds);
            var facts = new HitFacts(victim.Opponent(), BodyPart.Torso, WeaponIds.Rapier, time, false);
            return new HitLandedEvent(time, facts, new DamageResult(hpDamage, hpDamage, 0f, false, false));
        }

        /// <summary>A <see cref="Seconds"/>-long turn whose resolved board has taken every hit's and wall zap's damage.</summary>
        public static TurnResult Create(RulesSettings settings, int turnIndex, params MatchEvent[] events)
        {
            BoardSnapshot board = TestData.CreateInitialBoard(settings);
            board.State.TurnIndex = turnIndex;
            var recorder = new TimelineRecorder();
            recorder.RecordFrame(0, SimTime.Zero, new PoseSnapshot());
            recorder.RecordFrame(1, SimTime.FromSeconds(Seconds), new PoseSnapshot());
            foreach (MatchEvent matchEvent in events)
            {
                if (matchEvent is HitLandedEvent hit) board.State.Fighters[hit.Hit.Victim].Hp -= hit.Damage.HpDamage;
                if (matchEvent is ElectricWallEvent zap) board.State.Fighters[zap.Side].Hp -= zap.HpDamage;
                recorder.RecordEvent(matchEvent);
            }

            var report = new ExecutionReport(board.State, new PerSide<SimTime?>(null, null), false);
            return new TurnResult(turnIndex, recorder.Build(), board, report);
        }
    }
}
