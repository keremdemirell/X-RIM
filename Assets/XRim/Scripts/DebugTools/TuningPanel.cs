using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using XRim.Config;

namespace XRim.DebugTools
{
    /// <summary>
    /// Runtime tuning panel over every asset in a <see cref="TuningProfile"/>. Values are edited on the live assets:
    /// they apply from the next match snapshot, and in the Editor they stay after leaving Play mode.
    /// Orange = placeholder value (not from the GDD); [TBD] = an open design question.
    /// </summary>
    public sealed class TuningPanel
    {
        private const float LabelWidth = 250f;
        private static readonly Color PlaceholderColor = new Color(1f, 0.6f, 0.2f);

        private readonly HashSet<string> _expanded = new HashSet<string>();
        private readonly Dictionary<string, string> _textBuffers = new Dictionary<string, string>();

        public void Draw(TuningProfile profile)
        {
            GUILayout.Label("Edits apply from the next match. Orange = placeholder, [TBD] = open question.");
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
            bool expanded = _expanded.Contains(key);
            if (GUILayout.Button((expanded ? "▼ " : "► ") + asset.name))
            {
                if (expanded) _expanded.Remove(key);
                else _expanded.Add(key);
                expanded = !expanded;
            }

            if (!expanded) return;

            bool changed = false;
            foreach (TunableField field in TunableFields.Describe(targetType))
            {
                changed |= DrawField(key, target, field);
            }

            if (changed) MarkDirty(asset);
        }

        private bool DrawField(string sectionKey, object target, TunableField field)
        {
            Color previous = GUI.contentColor;
            if (field.IsPlaceholder) GUI.contentColor = PlaceholderColor;

            GUILayout.BeginHorizontal();
            GUILayout.Label(new GUIContent(field.Label + (field.IsTbd ? " [TBD]" : string.Empty), field.Describe()),
                GUILayout.Width(LabelWidth));

            object value = field.Field.GetValue(target);
            object edited = value;
            string bufferKey = sectionKey + "/" + field.Field.Name;
            switch (value)
            {
                case float number:
                    if (TryEditText(bufferKey, number.ToString("R", CultureInfo.InvariantCulture), out string floatText) &&
                        float.TryParse(floatText, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsedFloat))
                    {
                        edited = parsedFloat;
                    }

                    break;
                case int number:
                    if (TryEditText(bufferKey, number.ToString(CultureInfo.InvariantCulture), out string intText) &&
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

        /// <summary>Text fields keep their own buffer so partially typed numbers ("1.") are not reformatted mid-edit.</summary>
        private bool TryEditText(string key, string current, out string text)
        {
            if (!_textBuffers.TryGetValue(key, out string buffer)) buffer = current;
            text = GUILayout.TextField(buffer);
            _textBuffers[key] = text;
            return text != current;
        }

        private static object NextEnumValue(Enum current)
        {
            Array values = Enum.GetValues(current.GetType());
            int index = Array.IndexOf(values, current);
            return values.GetValue((index + 1) % values.Length);
        }

        private static void MarkDirty(ScriptableObject asset)
        {
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(asset);
#endif
        }
    }
}
