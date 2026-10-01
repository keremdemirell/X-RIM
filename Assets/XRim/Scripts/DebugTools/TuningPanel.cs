using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using XRim.Config;
using XRim.Core;

namespace XRim.DebugTools
{
    /// <summary>
    /// Runtime tuning panel over every asset in a <see cref="TuningProfile"/>. Values are edited on the live assets:
    /// they apply from the next match snapshot (in the sandbox, simulation values from the next Execute), and in the Editor they stay after
    /// leaving Play mode. Nested settings groups fold open. Orange = placeholder value (not from the GDD);
    /// [TBD] = an open design question.
    /// </summary>
    public sealed class TuningPanel
    {
        private const float LabelWidth = 250f;
        private const float IndentWidth = 16f;
        private static readonly Color PlaceholderColor = new Color(1f, 0.6f, 0.2f);

        private readonly HashSet<string> _expanded = new HashSet<string>();
        private readonly Dictionary<string, string> _textBuffers = new Dictionary<string, string>();

        public void Draw(TuningProfile profile)
        {
            GUILayout.Label("Edits apply from the next match (sandbox: simulation values from the next Execute). Orange = placeholder, [TBD] = open question.");
            foreach (SettingsConfigBase config in profile.SettingsConfigs())
            {
                DrawSection(config, config.SettingsObject, config.SettingsType);
            }

            foreach (ScriptableObject config in profile.ClientConfigs())
            {
                DrawSection(config, config, config.GetType());
            }
        }

        private void DrawSection(ScriptableObject asset, object target, Type targetType)
        {
            string key = asset.GetType().Name + "/" + asset.name;
            if (!Foldout(key, asset.name, 0, GUI.skin.button)) return;

            bool changed = false;
            foreach (TunableField field in TunableFields.Describe(targetType))
            {
                changed |= DrawField(key, target, field, 0);
            }

            if (changed) DebugAssets.MarkDirty(asset);
        }

        private bool DrawField(string parentKey, object target, TunableField field, int depth)
        {
            object value = field.Field.GetValue(target);
            string key = parentKey + "/" + field.Field.Name;
            if (value != null && IsNestedSettings(field.Field.FieldType)) return DrawNested(key, value, field, depth);

            Color previous = GUI.contentColor;
            if (field.IsPlaceholder) GUI.contentColor = PlaceholderColor;

            GUILayout.BeginHorizontal();
            GUILayout.Space(depth * IndentWidth);
            GUILayout.Label(new GUIContent(field.Label + (field.IsTbd ? " [TBD]" : string.Empty), field.Describe()),
                GUILayout.Width(LabelWidth - depth * IndentWidth));

            object edited = value;
            switch (value)
            {
                case float number:
                    edited = EditFloat(key, number);
                    break;
                case int number:
                    if (TryEditText(key, number.ToString(CultureInfo.InvariantCulture), out string intText) &&
                        int.TryParse(intText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedInt))
                    {
                        edited = parsedInt;
                    }

                    break;
                case bool flag:
                    edited = GUILayout.Toggle(flag, flag ? "on" : "off");
                    break;
                case Enum choice:
                    if (GUILayout.Button(choice.ToString())) edited = NextEnumValue(choice);
                    break;
                case Vec2 vector:
                    edited = new Vec2(EditFloat(key + ".x", vector.X), EditFloat(key + ".y", vector.Y));
                    break;
                case ICollection collection:
                    GUILayout.Label($"{collection.Count} item(s)");
                    break;
                default:
                    GUILayout.Label(value != null ? value.ToString() : "null");
                    break;
            }

            GUILayout.EndHorizontal();
            GUI.contentColor = previous;

            if (Equals(edited, value)) return false;
            field.Field.SetValue(target, edited);
            return true;
        }

        /// <summary>A nested settings group (for example the weapon motor inside the simulation settings), edited in place.</summary>
        private bool DrawNested(string key, object value, TunableField field, int depth)
        {
            if (!Foldout(key, field.Label, depth, GUI.skin.label)) return false;

            bool changed = false;
            foreach (TunableField child in TunableFields.Describe(value.GetType()))
            {
                changed |= DrawField(key, value, child, depth + 1);
            }

            return changed;
        }

        private bool Foldout(string key, string label, int depth, GUIStyle style)
        {
            bool expanded = _expanded.Contains(key);
            GUILayout.BeginHorizontal();
            GUILayout.Space(depth * IndentWidth);
            if (GUILayout.Button((expanded ? "▼ " : "► ") + label, style))
            {
                if (expanded) _expanded.Remove(key);
                else _expanded.Add(key);
                expanded = !expanded;
            }

            GUILayout.EndHorizontal();
            return expanded;
        }

        private float EditFloat(string key, float value)
        {
            return TryEditText(key, value.ToString("R", CultureInfo.InvariantCulture), out string text) &&
                   float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed)
                ? parsed
                : value;
        }

        /// <summary>Text fields keep their own buffer so partially typed numbers ("1.") are not reformatted mid-edit.</summary>
        private bool TryEditText(string key, string current, out string text)
        {
            if (!_textBuffers.TryGetValue(key, out string buffer)) buffer = current;
            text = GUILayout.TextField(buffer);
            _textBuffers[key] = text;
            return text != current;
        }

        private static bool IsNestedSettings(Type type) =>
            type.IsClass && type != typeof(string) && !typeof(IEnumerable).IsAssignableFrom(type) &&
            type.IsDefined(typeof(SerializableAttribute), false);

        private static object NextEnumValue(Enum current)
        {
            Array values = Enum.GetValues(current.GetType());
            int index = Array.IndexOf(values, current);
            return values.GetValue((index + 1) % values.Length);
        }
    }
}
