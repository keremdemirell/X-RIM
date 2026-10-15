using System;
using System.Collections;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using XRim.Core;
using XRim.Rules;
using XRim.Rules.Events;
using XRim.Rules.Match;
using XRim.Simulation.Recording;

namespace XRim.Tests.EditMode.Simulation
{
    /// <summary>
    /// Everything a <see cref="TurnResult"/> holds, written out as text with exact floats: its events (every public value, recursively),
    /// every recorded pose, the execution report and the final board. Two results with the same fingerprint are the same outcome, so
    /// tests can show that something (the feel layer, playback) never changed or never depended on a turn.
    /// </summary>
    internal static class TurnFingerprint
    {
        private const int MaxDepth = 6;
        private const BindingFlags Public = BindingFlags.Instance | BindingFlags.Public;

        public static string Of(TurnResult turn)
        {
            var text = new StringBuilder();
            text.Append("turn ").Append(turn.TurnIndex).Append(' ').Append(turn.EndReason).AppendLine();
            foreach (MatchEvent matchEvent in turn.Timeline.Events)
            {
                text.Append("event ").AppendLine(Describe(matchEvent, MaxDepth));
            }

            foreach (TimelineFrame frame in turn.Timeline.Frames)
            {
                text.Append("frame ").Append(frame.Step).Append(' ').Append(frame.Time.Microseconds).Append(' ')
                    .AppendLine(Describe(frame.Pose, MaxDepth));
            }

            ExecutionReport report = turn.Report;
            text.Append("report ").Append(Describe(report.FirstValidHitTime, MaxDepth)).Append(' ').Append(report.FirstHitsOnSameStep)
                .Append(' ').AppendLine(Describe(report.DeathTime, MaxDepth));
            text.Append("resolved ").AppendLine(State(report.ResolvedState));
            text.Append("final ").Append(State(turn.FinalBoard.State)).Append(' ').AppendLine(Describe(turn.FinalBoard.Pose, MaxDepth));
            return text.ToString();
        }

        /// <summary>The state's public values plus each fighter's limb damage and severed limbs (kept privately).</summary>
        private static string State(MatchState state)
        {
            var text = new StringBuilder(Describe(state, MaxDepth));
            foreach (Side side in new[] { Side.Left, Side.Right })
            {
                FighterState fighter = state.Fighters[side];
                text.Append(' ').Append(side).Append(" limbs");
                foreach (BodyPart part in Enum.GetValues(typeof(BodyPart)))
                {
                    text.Append(' ').Append(Exact(fighter.GetLimbDamage(part))).Append(fighter.IsSevered(part) ? "x" : string.Empty);
                }
            }

            return text.ToString();
        }

        private static string Describe(object value, int depth)
        {
            switch (value)
            {
                case null:
                    return "null";
                case string text:
                    return text;
                case float number:
                    return Exact(number);
                case double number:
                    return number.ToString("R", CultureInfo.InvariantCulture);
                case SimTime time:
                    return time.Microseconds.ToString(CultureInfo.InvariantCulture);
                case Vec2 vector:
                    return "(" + Exact(vector.X) + "," + Exact(vector.Y) + ")";
                case Enum choice:
                    return choice.ToString();
                case IFormattable primitive when value.GetType().IsPrimitive:
                    return primitive.ToString(null, CultureInfo.InvariantCulture);
                case bool flag:
                    return flag ? "true" : "false";
                case IEnumerable items:
                    return "[" + string.Join(";", items.Cast<object>().Select(item => Describe(item, depth - 1))) + "]";
            }

            Type type = value.GetType();
            if (depth <= 0) return type.Name;

            var members = new StringBuilder(type.Name).Append('{');
            foreach (FieldInfo field in type.GetFields(Public).OrderBy(f => f.Name, StringComparer.Ordinal))
            {
                members.Append(field.Name).Append('=').Append(Describe(field.GetValue(value), depth - 1)).Append(' ');
            }

            foreach (PropertyInfo property in type.GetProperties(Public).OrderBy(p => p.Name, StringComparer.Ordinal))
            {
                if (property.GetIndexParameters().Length > 0 || !property.CanRead) continue;
                members.Append(property.Name).Append('=').Append(Read(property, value, depth - 1)).Append(' ');
            }

            return members.Append('}').ToString();
        }

        private static string Read(PropertyInfo property, object owner, int depth)
        {
            try
            {
                return Describe(property.GetValue(owner), depth);
            }
            catch (TargetInvocationException)
            {
                return "!";
            }
        }

        private static string Exact(float number) => number.ToString("R", CultureInfo.InvariantCulture);
    }
}
