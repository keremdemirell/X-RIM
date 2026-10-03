using System.Collections.Generic;
using System.Globalization;
using XRim.Rules.Damage;
using XRim.Rules.Events;

namespace XRim.DebugTools
{
    /// <summary>
    /// One line per rules outcome for the debug panels (Session 06): a landed hit with its damage, a cancelled attack, a stun, a
    /// death. Raw contacts and body moves are not outcomes, so they get no line here.
    /// </summary>
    internal static class MatchEventText
    {
        public static bool IsOutcome(MatchEvent matchEvent) =>
            matchEvent is HitLandedEvent || matchEvent is AttackInterruptedEvent || matchEvent is StatusAppliedEvent ||
            matchEvent is FighterDiedEvent;

        public static string Describe(MatchEvent matchEvent)
        {
            string time = matchEvent.Time.Milliseconds.ToString("0.0", CultureInfo.InvariantCulture) + " ms   ";
            switch (matchEvent)
            {
                case HitLandedEvent hit:
                    return time + DescribeHit(hit);
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
