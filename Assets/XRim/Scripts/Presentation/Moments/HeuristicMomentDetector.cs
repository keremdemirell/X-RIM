using System.Collections.Generic;
using XRim.Core;
using XRim.Core.Gdd;
using XRim.Presentation.Feel;
using XRim.Presentation.Playback;
using XRim.Rules;
using XRim.Rules.Combat;
using XRim.Rules.Events;
using XRim.Simulation.Recording;

namespace XRim.Presentation.Moments
{
    /// <summary>
    /// The default way of spotting legendary turns (GDD pillar 1; §17 replays TBD: a local instant replay only, designer 2026-10-11). It
    /// reads only the played turn's result (its events and poses), so any turn, local or from a server, is judged the same. In order of
    /// importance, the first that applies wins:
    /// <list type="number">
    /// <item><b>Sudden-death finish</b> (§14): the turn's first decisive hit or death.</item>
    /// <item><b>Double KO</b>: both dummies die in a normal turn (§3: to sudden death).</item>
    /// <item><b>KO</b>: one dummy dies, the match-winning blow (designer: always replayed).</item>
    /// <item><b>Sever</b> (§12).</item>
    /// <item><b>Dodge and counter</b>, the §1 reference moment: one dummy crouches, jumps or steps back while the other swings (a weapon,
    /// not a raised shield), lands its own hit, and takes none.</item>
    /// <item><b>Counter-hit</b> (§9): a hit cancels the other dummy's attack and that dummy lands nothing.</item>
    /// <item><b>Trade</b>: both land a hit at exactly the same instant.</item>
    /// <item><b>Crush</b> (§10): a weapon crushes through the other.</item>
    /// </list>
    /// Each kind can be switched off (<see cref="MomentSettings"/>). Pure: tested without Unity.
    /// </summary>
    [GddTbd("§17", "Replays and clip sharing", Proposal = "Local instant replay only (designer 2026-10-11)")]
    public sealed class HeuristicMomentDetector : IMomentDetector
    {
        private static readonly Side[] BothSides = { Side.Left, Side.Right };

        private readonly MomentSettings _settings;

        public HeuristicMomentDetector(MomentSettings settings = null)
        {
            _settings = settings ?? new MomentSettings();
        }

        public bool TryDetect(TurnResult turn, out LegendaryMoment moment)
        {
            Guard.NotNull(turn, nameof(turn));
            return (_settings.SuddenDeathFinishes && TrySuddenDeathFinish(turn, out moment))
                   || (_settings.KnockOuts && TryKnockOut(turn, out moment))
                   || (_settings.Severs && TrySever(turn, out moment))
                   || (_settings.DodgeCounters && TryDodgeCounter(turn, out moment))
                   || (_settings.CounterHits && TryCounterHit(turn, out moment))
                   || (_settings.Trades && TryTrade(turn, out moment))
                   || (_settings.Crushes && TryCrush(turn, out moment))
                   || Nothing(out moment);
        }

        private static bool TrySuddenDeathFinish(TurnResult turn, out LegendaryMoment moment)
        {
            moment = default;
            if (!SlowMotionPlanner.TryFindSuddenDeathFinish(turn, out SimTime time)) return false;

            // The side whose hit landed then, or whose opponent died then; both at the same instant is nobody's alone (a tie, §14).
            int decisive = 0;
            Side winner = Side.Left;
            foreach (Side side in BothSides)
            {
                if (turn.Report.FirstValidHitTime[side] != time && turn.Report.DeathTime[side.Opponent()] != time) continue;
                decisive++;
                winner = side;
            }

            Side? hero = decisive == 1 ? winner : (Side?)null;

            Vec2 focus = Focus(turn, FirstOf<HitLandedEvent>(turn, e => e.Time == time) ?? (MatchEvent)FirstOf<FighterDiedEvent>(turn, e => e.Time == time));
            moment = new LegendaryMoment(MomentKind.SuddenDeathFinish, time, hero, focus,
                hero.HasValue ? $"{hero.Value} won sudden death" : "Sudden death ended at exactly the same instant");
            return true;
        }

        private static bool TryKnockOut(TurnResult turn, out LegendaryMoment moment)
        {
            moment = default;
            if (turn.Report.ResolvedState.IsSuddenDeath) return false;
            var deaths = new List<FighterDiedEvent>();
            foreach (MatchEvent matchEvent in turn.Timeline.Events)
            {
                if (matchEvent is FighterDiedEvent died) deaths.Add(died);
            }

            if (deaths.Count == 0) return false;
            if (deaths.Count == 1)
            {
                FighterDiedEvent died = deaths[0];
                moment = new LegendaryMoment(MomentKind.KnockOut, died.Time, died.Side.Opponent(), Focus(turn, died),
                    $"{died.Side.Opponent()} knocked {died.Side} out");
                return true;
            }

            FighterDiedEvent last = deaths[deaths.Count - 1];
            Vec2 middle = (Focus(turn, deaths[0]) + Focus(turn, last)) * 0.5f;
            moment = new LegendaryMoment(MomentKind.DoubleKnockOut, last.Time, null, middle, "Both dummies knocked out in one turn");
            return true;
        }

        private static bool TrySever(TurnResult turn, out LegendaryMoment moment)
        {
            moment = default;
            LimbSeveredEvent severed = FirstOf<LimbSeveredEvent>(turn, _ => true);
            if (severed == null) return false;
            moment = new LegendaryMoment(MomentKind.Sever, severed.Time, severed.Side.Opponent(), Focus(turn, severed),
                $"{severed.Side.Opponent()} cut off {severed.Side}'s {severed.Part}");
            return true;
        }

        /// <summary>The §1 reference: out of the other's swing (a crouch, a jump, a step back) and in with a hit, taking none.</summary>
        private static bool TryDodgeCounter(TurnResult turn, out LegendaryMoment moment)
        {
            moment = default;
            foreach (Side side in BothSides)
            {
                BodyMoveStartedEvent move = FirstOf<BodyMoveStartedEvent>(turn, e => e.Side == side);
                if (move == null || !IsEvasive(move.Move)) continue;
                Side other = side.Opponent();
                if (FirstOf<AttackStartedEvent>(turn, e => e.Side == other && e.Kind != WeaponKind.Shield) == null) continue;
                HitLandedEvent counter = FirstOf<HitLandedEvent>(turn, e => e.Hit.Attacker == side);
                if (counter == null || FirstOf<HitLandedEvent>(turn, e => e.Hit.Attacker == other) != null) continue;

                moment = new LegendaryMoment(MomentKind.DodgeCounter, counter.Time, side, Focus(turn, counter), $"{side} {Dodge(move.Move)} and countered");
                return true;
            }

            return false;
        }

        private static bool TryCounterHit(TurnResult turn, out LegendaryMoment moment)
        {
            moment = default;
            foreach (MatchEvent matchEvent in turn.Timeline.Events)
            {
                if (!(matchEvent is AttackInterruptedEvent interrupted)) continue;
                Side victim = interrupted.Interrupted;
                if (FirstOf<HitLandedEvent>(turn, e => e.Hit.Attacker == victim) != null) continue; // it had landed its own: no clean counter

                moment = new LegendaryMoment(MomentKind.CounterHit, interrupted.Time, victim.Opponent(), Focus(turn, interrupted),
                    $"{victim.Opponent()} hit first and cancelled {victim}'s attack");
                return true;
            }

            return false;
        }

        private static bool TryTrade(TurnResult turn, out LegendaryMoment moment)
        {
            moment = default;
            foreach (MatchEvent matchEvent in turn.Timeline.Events)
            {
                if (!(matchEvent is HitLandedEvent hit) || hit.Hit.Attacker != Side.Left) continue;
                HitLandedEvent answer = FirstOf<HitLandedEvent>(turn, e => e.Hit.Attacker == Side.Right && e.Time == hit.Time);
                if (answer == null) continue;

                moment = new LegendaryMoment(MomentKind.Trade, hit.Time, null, (Focus(turn, hit) + Focus(turn, answer)) * 0.5f,
                    "Both landed at exactly the same instant");
                return true;
            }

            return false;
        }

        private static bool TryCrush(TurnResult turn, out LegendaryMoment moment)
        {
            moment = default;
            WeaponClashEvent crush = FirstOf<WeaponClashEvent>(turn, e => e.Result.Kind == ClashKind.CrushThrough);
            if (crush == null) return false;
            moment = new LegendaryMoment(MomentKind.Crush, crush.Time, crush.Result.Winner, crush.ContactPointUnits,
                $"{crush.Result.Winner} crushed through");
            return true;
        }

        private static bool Nothing(out LegendaryMoment moment)
        {
            moment = default;
            return false;
        }

        private static bool IsEvasive(BodyMove move) => move == BodyMove.Crouch || move == BodyMove.Jump || move == BodyMove.StepBack;

        private static string Dodge(BodyMove move)
        {
            switch (move)
            {
                case BodyMove.Crouch: return "ducked under the swing";
                case BodyMove.Jump: return "jumped the swing";
                default: return "slipped back from the swing";
            }
        }

        private static T FirstOf<T>(TurnResult turn, System.Func<T, bool> where) where T : MatchEvent
        {
            foreach (MatchEvent matchEvent in turn.Timeline.Events)
            {
                if (matchEvent is T typed && where(typed)) return typed;
            }

            return null;
        }

        private static Vec2 Focus(TurnResult turn, MatchEvent matchEvent) =>
            matchEvent != null && MatchEventLocator.TryLocate(turn, matchEvent, out Vec2 point) ? point : Vec2.Zero;
    }
}
