using System.Globalization;
using XRim.Rules.Match;
using XRim.Simulation.Execution;
using XRim.Simulation.Physics;

namespace XRim.DebugTools
{
    /// <summary>
    /// One line per contact for the debug panels: refined time-to-impact (GDD §9), the step the engine reported it in, what
    /// touched what, the contact angle (§10 stage 1: 0° slides, 90° is square), the relative speed and the weapon's
    /// distance along its path (d = v·t).
    /// </summary>
    internal static class ContactText
    {
        public const string Header = "time (step)   first → second   angle   speed   path distance";

        public static string Describe(TurnContact contact, MatchState state)
        {
            ContactFacts facts = contact.Facts;
            string distance = contact.WeaponSide.HasValue ? contact.PathDistanceUnits.ToString("0", CultureInfo.InvariantCulture) : "–";
            return string.Format(CultureInfo.InvariantCulture, "{0:0.0} ms ({1})   {2} → {3}   {4:0}°   {5:0} u/s   d {6}",
                contact.Time.Milliseconds, contact.ReportedStep, Name(facts.A, state), Name(facts.B, state), contact.ContactAngleDegrees,
                contact.RelativeSpeedUnitsPerSecond, distance);
        }

        private static string Name(BodyTag tag, MatchState state)
        {
            string owner = tag.Owner.HasValue ? tag.Owner.Value.ToString() : "Arena";
            switch (tag.Role)
            {
                case BodyRole.HeldItem:
                    return tag.Owner.HasValue ? $"{owner} {state.Fighters[tag.Owner.Value].CurrentWeapon.Value}" : "held item";
                case BodyRole.BodyPart:
                    return $"{owner} {tag.Part}";
                case BodyRole.SeveredLimb:
                    return $"{owner} severed {tag.Part}";
                default:
                    return $"{owner} {tag.Role}";
            }
        }
    }
}
