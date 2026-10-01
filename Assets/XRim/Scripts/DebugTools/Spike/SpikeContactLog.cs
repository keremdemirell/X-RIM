using System.Globalization;
using UnityEngine;
using XRim.Core;
using XRim.Rules.Match;
using XRim.Simulation.Execution;
using XRim.Simulation.Physics;

namespace XRim.DebugTools.Spike
{
    /// <summary>
    /// On-screen log of the last swing's contacts, in the order they happened: refined time-to-impact (GDD §9), the step
    /// the engine reported it in, what touched what, the contact angle (§10 stage 1: 0° slides, 90° is square), the
    /// relative speed, the normal (from the first body to the second) and the weapon's distance along its path (d = v·t).
    /// During playback, contacts the playback has reached are marked.
    /// </summary>
    internal sealed class SpikeContactLog
    {
        private const float Padding = 6f;
        private const float LineHeight = 18f;
        private const int HeaderLines = 2;
        private const string ReachedMark = "► ";
        private const string PendingMark = "   ";

        private Vector2 _scroll;

        public void Draw(Rect area, SwingResult result, MatchState state, SimTime playbackTime, bool playing)
        {
            GUI.Box(area, GUIContent.none);
            var inner = new Rect(area.x + Padding, area.y + Padding, area.width - Padding * 2f, area.height - Padding * 2f);
            GUILayout.BeginArea(inner);
            GUILayout.Label(result == null
                ? "Contacts: none yet. Draw a path and press Space."
                : $"Contacts of the last swing: {result.Contacts.Count} (earliest first; times refined inside the step)");
            GUILayout.Label("time (step)   weapon/part → part   angle   relative speed   normal   path distance");
            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(Mathf.Max(0f, inner.height - LineHeight * HeaderLines)));
            if (result != null)
            {
                for (int i = 0; i < result.Contacts.Count; i++)
                {
                    SwingContact contact = result.Contacts[i];
                    bool reached = playing && contact.Time <= playbackTime;
                    GUILayout.Label((reached ? ReachedMark : PendingMark) + Describe(contact, state));
                }
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private static string Describe(SwingContact contact, MatchState state)
        {
            ContactFacts facts = contact.Facts;
            string distance = contact.WeaponSide.HasValue ? contact.PathDistanceUnits.ToString("0.0", CultureInfo.InvariantCulture) : "–";
            return string.Format(CultureInfo.InvariantCulture,
                "{0:0.000} ms ({1})   {2} → {3}   {4:0}°   {5:0} u/s   ({6:0.00}, {7:0.00})   d {8}",
                contact.Time.Milliseconds, contact.ReportedStep, Name(facts.A, state), Name(facts.B, state), contact.ContactAngleDegrees,
                contact.RelativeSpeedUnitsPerSecond, facts.Normal.X, facts.Normal.Y, distance);
        }

        private static string Name(BodyTag tag, MatchState state)
        {
            string owner = tag.Owner.HasValue ? tag.Owner.Value.ToString() : "Arena";
            switch (tag.Role)
            {
                case BodyRole.HeldItem:
                    return tag.Owner.HasValue ? $"{owner} {state.Fighters[tag.Owner.Value].CurrentWeapon.Value}" : "held item";
                case BodyRole.BodyPart:
                case BodyRole.SeveredLimb:
                    return $"{owner} {tag.Part}";
                default:
                    return $"{owner} {tag.Role}";
            }
        }
    }
}
