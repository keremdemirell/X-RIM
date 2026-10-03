using System.Collections.Generic;
using System.Globalization;
using XRim.Core;
using XRim.Rules.Combat;
using XRim.Rules.Damage;
using XRim.Rules.Events;

namespace XRim.DebugTools
{
    /// <summary>
    /// One line per rules outcome for the debug panels (Sessions 06 and 07): a landed hit with its damage, a clash with its angle,
    /// powers and outcome, a shield block, a cancelled attack, a stun or stagger, a death. Raw contacts and body moves are not
    /// outcomes, so they get no line here.
    /// </summary>
    internal static class MatchEventText
    {
        public static bool IsOutcome(MatchEvent matchEvent) =>
            matchEvent is HitLandedEvent || matchEvent is WeaponClashEvent || matchEvent is ShieldBlockEvent ||
            matchEvent is AttackInterruptedEvent || matchEvent is StatusAppliedEvent || matchEvent is FighterDiedEvent;

        public static string Describe(MatchEvent matchEvent)
        {
            string time = matchEvent.Time.Milliseconds.ToString("0.0", CultureInfo.InvariantCulture) + " ms   ";
            switch (matchEvent)
            {
                case HitLandedEvent hit:
                    return time + DescribeHit(hit);
                case WeaponClashEvent clash:
                    return time + DescribeClash(clash.Result);
                case ShieldBlockEvent block:
                    return time + DescribeBlock(block);
                case AttackInterruptedEvent interrupted:
                    return time + $"{interrupted.Interrupted}'s attack is cancelled";
                case StatusAppliedEvent status:
                    return time + $"{status.Side} is {status.Kind.ToString().ToLowerInvariant()} for the next turn";
                case FighterDiedEvent died:
                    return time + $"{died.Side} is KO";
                default:
                    return time + matchEvent.GetType().Name;
            }
        }

        private static string DescribeClash(ClashResult result)
        {
            string outcome;
            switch (result.Kind)
            {
                case ClashKind.CrushThrough:
                    Side loser = result.Winner.Value.Opponent();
                    outcome = $"{result.Winner} crushes through (less damage on its later hits); {loser} is knocked off and staggered";
                    break;
                case ClashKind.BothRebound:
                    outcome = "both rebound";
                    break;
                case ClashKind.LighterDeflectsHeavier:
                    outcome = $"{result.Winner} deflects {result.Winner.Value.Opponent()}'s heavier weapon off its path";
                    break;
                default:
                    outcome = "both slide past";
                    break;
            }

            return string.Format(CultureInfo.InvariantCulture, "clash {0:0}° ({1}), power L {2:0.##} / R {3:0.##}: {4}", result.ContactAngleDegrees,
                result.IsHardClash ? "hard" : "glancing", result.LeftPower, result.RightPower, outcome);
        }

        private static string DescribeBlock(ShieldBlockEvent block)
        {
            BlockResult result = block.Result;
            string what = result.AttackStopped
                ? $"full block, {block.Attacker}'s attack stops"
                : string.Format(CultureInfo.InvariantCulture, "partial block, {0}'s later hits ×{1:0.##}", block.Attacker, result.DamageMultiplier);
            if (result.ShieldHolderStaggered) what += $", {block.Blocker} is staggered";
            return string.Format(CultureInfo.InvariantCulture, "{0}'s shield ({1:0}° to the face, at {2:0.00} of its half height): {3}", block.Blocker,
                block.ContactAngleDegrees, block.FacePositionFraction, what);
        }

        private static string DescribeHit(HitLandedEvent hit)
        {
            DamageResult damage = hit.Damage;
            var notes = new List<string>();
            if (damage.LimbDamage > 0f) notes.Add(string.Format(CultureInfo.InvariantCulture, "limb +{0:0.#}", damage.LimbDamage));
            if (damage.WasClampedByNoInstantKo)
                notes.Add(string.Format(CultureInfo.InvariantCulture, "no instant KO: {0:0.#} kept to {1:0.#}", damage.Damage, damage.HpDamage));
            if (damage.Stuns) notes.Add("STUN");
            if (damage.SeversLimb) notes.Add("limb at 0: would sever (Session 11)");
            string extra = notes.Count > 0 ? " (" + string.Join(", ", notes) + ")" : string.Empty;
            return string.Format(CultureInfo.InvariantCulture, "{0} {1} → {2} {3}: {4:0.#} HP{5}", hit.Hit.Attacker, hit.Hit.Weapon.Value,
                hit.Hit.Victim, hit.Hit.Part, damage.HpDamage, extra);
        }
    }
}
