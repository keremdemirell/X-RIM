using System;
using System.Globalization;
using XRim.Core;
using XRim.Rules;
using XRim.Rules.Arena;
using XRim.Rules.Combat;
using XRim.Rules.Events;
using XRim.Rules.Status;
using XRim.Simulation.Execution;

namespace XRim.Simulation.Scenarios
{
    /// <summary>
    /// One rules event of a recorded turn in plain fields, so a saved scenario can say what its turn did and a replay can be compared
    /// with it. The fields mean, by <see cref="Kind"/>:
    /// <list type="bullet">
    /// <item>BodyMoveStarted: <see cref="Side"/>, <see cref="Detail"/> = the <see cref="BodyMove"/>.</item>
    /// <item>AttackStarted: side, <see cref="Id"/> = weapon, detail = the <see cref="WeaponKind"/>.</item>
    /// <item>SignatureUsed: side, id = the move.</item>
    /// <item>HitLanded: side = attacker, <see cref="Part"/> = the part struck, id = weapon, <see cref="Amount"/> = damage,
    /// <see cref="SecondAmount"/> = limb damage, <see cref="Flag"/> = it stuns.</item>
    /// <item>WeaponClash: detail = the <see cref="ClashKind"/>, flag = one weapon came out ahead, side = that weapon's, amount = the angle.</item>
    /// <item>ShieldBlock: side = the blocker, amount = the angle, second amount = the damage share left, flag = a full block.</item>
    /// <item>AttackInterrupted: side = whose attack was cancelled.</item>
    /// <item>StatusApplied: side, detail = the <see cref="StatusKind"/>.</item>
    /// <item>LimbSevered: side = whose limb, part.</item>
    /// <item>WeaponDropped: side, id = the weapon.</item>
    /// <item>FighterDied: side.</item>
    /// <item>ElectricWall: side = whose wall, detail = the <see cref="ElectricWallPhase"/>, amount = HP damage, second amount = where the wall
    /// stands, flag = knocked into it.</item>
    /// <item>WeaponTraitHeal: side, id = the trait, amount = HP recovered.</item>
    /// <item>Other: id = the event's type name.</item>
    /// </list>
    /// </summary>
    [Serializable]
    public sealed class ScenarioEventRecord
    {
        public ScenarioEventKind Kind;
        public long TimeMicroseconds;
        public Side Side;
        public BodyPart Part;
        public string Id = string.Empty;
        public float Amount;
        public float SecondAmount;
        public int Detail;
        public bool Flag;

        /// <summary>The event in words, for people reading the file; never compared.</summary>
        public string Summary = string.Empty;

        /// <summary>
        /// The record of a rules event, or null for an event the scenario does not keep (the raw physics contacts, which the debug list
        /// shows: the rules' verdicts on them are what a scenario compares).
        /// </summary>
        public static ScenarioEventRecord Of(MatchEvent matchEvent)
        {
            Guard.NotNull(matchEvent, nameof(matchEvent));
            var record = new ScenarioEventRecord { TimeMicroseconds = matchEvent.Time.Microseconds };
            switch (matchEvent)
            {
                case ContactEvent _:
                    return null;
                case BodyMoveStartedEvent move:
                    record.Kind = ScenarioEventKind.BodyMoveStarted;
                    record.Side = move.Side;
                    record.Detail = (int)move.Move;
                    break;
                case AttackStartedEvent attack:
                    record.Kind = ScenarioEventKind.AttackStarted;
                    record.Side = attack.Side;
                    record.Id = attack.Weapon.Value ?? string.Empty;
                    record.Detail = (int)attack.Kind;
                    break;
                case SignatureUsedEvent signature:
                    record.Kind = ScenarioEventKind.SignatureUsed;
                    record.Side = signature.Side;
                    record.Id = signature.Move.Value ?? string.Empty;
                    break;
                case HitLandedEvent hit:
                    record.Kind = ScenarioEventKind.HitLanded;
                    record.Side = hit.Hit.Attacker;
                    record.Part = hit.Hit.Part;
                    record.Id = hit.Hit.Weapon.Value ?? string.Empty;
                    record.Amount = hit.Damage.Damage;
                    record.SecondAmount = hit.Damage.LimbDamage;
                    record.Flag = hit.Damage.Stuns;
                    break;
                case WeaponClashEvent clash:
                    record.Kind = ScenarioEventKind.WeaponClash;
                    record.Detail = (int)clash.Result.Kind;
                    record.Flag = clash.Result.Winner.HasValue;
                    record.Side = clash.Result.Winner ?? Side.Left;
                    record.Amount = clash.ContactAngleDegrees;
                    break;
                case ShieldBlockEvent block:
                    record.Kind = ScenarioEventKind.ShieldBlock;
                    record.Side = block.Blocker;
                    record.Amount = block.ContactAngleDegrees;
                    record.SecondAmount = block.Result.DamageMultiplier;
                    record.Flag = block.Result.AttackStopped;
                    break;
                case AttackInterruptedEvent interrupted:
                    record.Kind = ScenarioEventKind.AttackInterrupted;
                    record.Side = interrupted.Interrupted;
                    break;
                case StatusAppliedEvent status:
                    record.Kind = ScenarioEventKind.StatusApplied;
                    record.Side = status.Side;
                    record.Detail = (int)status.Kind;
                    break;
                case LimbSeveredEvent sever:
                    record.Kind = ScenarioEventKind.LimbSevered;
                    record.Side = sever.Side;
                    record.Part = sever.Part;
                    break;
                case WeaponDroppedEvent drop:
                    record.Kind = ScenarioEventKind.WeaponDropped;
                    record.Side = drop.Side;
                    record.Id = drop.Weapon.Value ?? string.Empty;
                    break;
                case FighterDiedEvent death:
                    record.Kind = ScenarioEventKind.FighterDied;
                    record.Side = death.Side;
                    break;
                case ElectricWallEvent wall:
                    record.Kind = ScenarioEventKind.ElectricWall;
                    record.Side = wall.Side;
                    record.Detail = (int)wall.Phase;
                    record.Amount = wall.HpDamage;
                    record.SecondAmount = wall.PositionUnits;
                    record.Flag = wall.IsKnockedInto;
                    break;
                case WeaponTraitHealEvent heal:
                    record.Kind = ScenarioEventKind.WeaponTraitHeal;
                    record.Side = heal.Side;
                    record.Id = heal.TraitId ?? string.Empty;
                    record.Amount = heal.HpRecovered;
                    break;
                default:
                    record.Kind = ScenarioEventKind.Other;
                    record.Id = matchEvent.GetType().Name;
                    break;
            }

            record.Summary = record.Describe();
            return record;
        }

        /// <summary>The event in words ("120.4 ms: Left hits Right's Torso with rapier, 8 damage").</summary>
        public string Describe()
        {
            CultureInfo c = CultureInfo.InvariantCulture;
            string at = (TimeMicroseconds / (double)SimTime.MicrosecondsPerMillisecond).ToString("0.0", c) + " ms: ";
            switch (Kind)
            {
                case ScenarioEventKind.BodyMoveStarted:
                    return at + Side + " " + (BodyMove)Detail;
                case ScenarioEventKind.AttackStarted:
                    return at + Side + " swings " + Id;
                case ScenarioEventKind.SignatureUsed:
                    return at + Side + " plays signature move " + Id;
                case ScenarioEventKind.HitLanded:
                    return at + string.Format(c, "{0} hits {1}'s {2} with {3}, {4:0.##} damage{5}{6}", Side, Side.Opponent(), Part, Id, Amount,
                        SecondAmount > 0f ? string.Format(c, " ({0:0.##} to the limb)", SecondAmount) : string.Empty, Flag ? ", stuns" : string.Empty);
                case ScenarioEventKind.WeaponClash:
                    return at + string.Format(c, "clash {0} at {1:0.#}°{2}", (ClashKind)Detail, Amount, Flag ? ", " + Side + "'s weapon ahead" : string.Empty);
                case ScenarioEventKind.ShieldBlock:
                    return at + string.Format(c, "{0} blocks {1}'s weapon ({2}, {3:0.#}°)", Side, Side.Opponent(), Flag ? "full" : "partial", Amount);
                case ScenarioEventKind.AttackInterrupted:
                    return at + Side + "'s attack is cancelled";
                case ScenarioEventKind.StatusApplied:
                    return at + Side + " " + (StatusKind)Detail;
                case ScenarioEventKind.LimbSevered:
                    return at + Side + "'s " + Part + " is severed";
                case ScenarioEventKind.WeaponDropped:
                    return at + Side + " drops " + Id;
                case ScenarioEventKind.FighterDied:
                    return at + Side + " dies";
                case ScenarioEventKind.ElectricWall:
                    return at + string.Format(c, "{0}'s wall {1}, {2:0.##} damage, at {3:0.#}{4}", Side, (ElectricWallPhase)Detail, Amount, SecondAmount,
                        Flag ? ", knocked into" : string.Empty);
                case ScenarioEventKind.WeaponTraitHeal:
                    return at + string.Format(c, "{0} recovers {1:0.##} HP ({2})", Side, Amount, Id);
                default:
                    return at + Id;
            }
        }
    }
}
