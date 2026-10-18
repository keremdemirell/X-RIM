using System;
using System.Collections.Generic;
using XRim.Core;
using XRim.Rules;
using XRim.Rules.Arena;
using XRim.Rules.Events;
using XRim.Rules.Match;
using XRim.Simulation.Recording;

namespace XRim.Bots.Balance
{
    /// <summary>
    /// What one bot-vs-bot match of the balance batch did, read from its turns' events (Session 16): who played which side with which
    /// loadout, how it ended and after how many turns, each side's weapons (swings, hits, damage, severs, the winning blow), severs, wall
    /// zaps, signature moves, and how long it would take on screen.
    /// </summary>
    public sealed class BalanceMatchRecord
    {
        private readonly PerSide<Dictionary<WeaponId, WeaponTally>> _weapons =
            PerSide<Dictionary<WeaponId, WeaponTally>>.Create(_ => new Dictionary<WeaponId, WeaponTally>());

        private readonly Dictionary<SignatureMoveId, int> _signatureUses = new Dictionary<SignatureMoveId, int>();
        private readonly Dictionary<SignatureMoveId, int> _signatureHitTurns = new Dictionary<SignatureMoveId, int>();
        private readonly List<double> _simulationMilliseconds = new List<double>();

        public BalanceMatchRecord(int seed, BalanceRun run, PerSide<BotBrainKind> brains, PerSide<IReadOnlyList<WeaponId>> loadouts)
        {
            Seed = seed;
            Run = Guard.NotNull(run, nameof(run));
            Brains = Guard.NotNull(brains, nameof(brains));
            Loadouts = Guard.NotNull(loadouts, nameof(loadouts));
        }

        public int Seed { get; }
        public BalanceRun Run { get; }
        public PerSide<BotBrainKind> Brains { get; }
        public PerSide<IReadOnlyList<WeaponId>> Loadouts { get; }

        /// <summary>Null while it runs, or when it hit the batch's turn limit without ending.</summary>
        public MatchOutcome Outcome { get; private set; }

        public bool Finished => Outcome != null;
        public int Turns { get; private set; }
        public bool ReachedSuddenDeath { get; private set; }
        public int WallZaps { get; private set; }
        public int ArmSevers { get; private set; }
        public int LegSevers { get; private set; }
        public int Severs => ArmSevers + LegSevers;

        /// <summary>The losing dummy died to its own electric wall, not a hit (§13).</summary>
        public bool EndedByWall { get; private set; }

        /// <summary>The recorded turns' length, as simulated.</summary>
        public double ExecutedSeconds { get; private set; }

        /// <summary>The turns' time on screen with the feel layer (hitstops, slow motion), or their recorded length without it.</summary>
        public double OnScreenSeconds { get; private set; }

        public IReadOnlyList<double> SimulationMilliseconds => _simulationMilliseconds;
        public IReadOnlyDictionary<SignatureMoveId, int> SignatureUses => _signatureUses;

        /// <summary>Turns in which a signature move was played and its side landed at least one hit.</summary>
        public IReadOnlyDictionary<SignatureMoveId, int> SignatureHitTurns => _signatureHitTurns;

        public IReadOnlyDictionary<WeaponId, WeaponTally> WeaponsOf(Side side) => _weapons[side];

        /// <summary>The brain that won, when the match ended.</summary>
        public BotBrainKind? WinningBrain => Outcome != null ? Brains[Outcome.Winner] : (BotBrainKind?)null;

        public void Add(TurnResult turn, double simulationMilliseconds, double onScreenSeconds)
        {
            Guard.NotNull(turn, nameof(turn));
            Turns++;
            ExecutedSeconds += turn.Timeline.Duration.Seconds;
            OnScreenSeconds += onScreenSeconds;
            _simulationMilliseconds.Add(simulationMilliseconds);
            if (turn.FinalBoard.State.IsSuddenDeath || turn.Report.ResolvedState.IsSuddenDeath) ReachedSuddenDeath = true;

            PerSide<SignatureMoveId> signature = PerSide<SignatureMoveId>.Create(_ => default);
            PerSide<WeaponId> swung = PerSide<WeaponId>.Create(_ => default);
            PerSide<bool> hit = PerSide<bool>.Create(_ => false);
            foreach (MatchEvent matchEvent in turn.Timeline.Events)
            {
                switch (matchEvent)
                {
                    case AttackStartedEvent attack:
                        Tally(attack.Side, attack.Weapon).Swings++;
                        swung[attack.Side] = attack.Weapon;
                        break;
                    case SignatureUsedEvent used:
                        signature[used.Side] = used.Move;
                        _signatureUses[used.Move] = Count(_signatureUses, used.Move) + 1;
                        break;
                    case HitLandedEvent landed:
                        WeaponTally tally = Tally(landed.Hit.Attacker, landed.Hit.Weapon);
                        tally.Hits++;
                        tally.Damage += landed.Damage.HpDamage;
                        if (landed.Damage.SeversLimb) tally.Severs++;
                        hit[landed.Hit.Attacker] = true;
                        break;
                    case LimbSeveredEvent severed:
                        if (severed.Part.IsArm()) ArmSevers++;
                        else LegSevers++;
                        break;
                    case ElectricWallEvent wall:
                        if (wall.Phase == ElectricWallPhase.Active && wall.HpDamage > 0f) WallZaps++;
                        break;
                }
            }

            foreach (Side side in new[] { Side.Left, Side.Right })
            {
                if (signature[side].IsEmpty) continue;
                if (!swung[side].IsEmpty) Tally(side, swung[side]).SignatureSwings++;
                if (!hit[side]) continue;
                _signatureHitTurns[signature[side]] = Count(_signatureHitTurns, signature[side]) + 1;
                if (!swung[side].IsEmpty) Tally(side, swung[side]).SignatureHitTurns++;
            }
        }

        /// <summary>The match ended: who won, and which weapon dealt the winning blow (the winner's last hit up to the loser's death in the last turn).</summary>
        public void Finish(MatchOutcome outcome, TurnResult lastTurn)
        {
            Outcome = Guard.NotNull(outcome, nameof(outcome));
            if (lastTurn == null || (outcome.Reason != MatchEndReason.KnockOut && outcome.Reason != MatchEndReason.SuddenDeathHit)) return;

            Side loser = outcome.Winner.Opponent();
            FighterDiedEvent death = null;
            foreach (MatchEvent matchEvent in lastTurn.Timeline.Events)
            {
                if (matchEvent is FighterDiedEvent died && died.Side == loser) death = died;
            }

            HitLandedEvent blow = null;
            foreach (MatchEvent matchEvent in lastTurn.Timeline.Events)
            {
                if (matchEvent is HitLandedEvent landed && landed.Hit.Attacker == outcome.Winner && (death == null || landed.Time <= death.Time)) blow = landed;
            }

            if (blow != null) Tally(outcome.Winner, blow.Hit.Weapon).Kos++;
            else if (death != null) EndedByWall = true;
        }

        private WeaponTally Tally(Side side, WeaponId weapon)
        {
            Dictionary<WeaponId, WeaponTally> weapons = _weapons[side];
            if (!weapons.TryGetValue(weapon, out WeaponTally tally))
            {
                tally = new WeaponTally();
                weapons.Add(weapon, tally);
            }

            return tally;
        }

        private static int Count(Dictionary<SignatureMoveId, int> counts, SignatureMoveId move) => counts.TryGetValue(move, out int count) ? count : 0;
    }
}
