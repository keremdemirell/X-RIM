using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using XRim.Config;

namespace XRim.Editor
{
    /// <summary>
    /// Inspector for every settings asset: shows the wrapped settings flat, tints placeholder values orange and marks
    /// GDD TBD fields, with the reason or open question in the tooltip.
    /// </summary>
    [CustomEditor(typeof(SettingsConfigBase), true)]
    public sealed class SettingsConfigEditor : UnityEditor.Editor
    {
        private const string Legend = "Orange = placeholder (the GDD has no value yet). [TBD] = open design question. Hover a field for details.";
        private static readonly Color PlaceholderTint = new Color(1f, 0.75f, 0.45f);

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var config = (SettingsConfigBase)target;
            EditorGUILayout.HelpBox(Legend, MessageType.None);

            SerializedProperty settings = serializedObject.FindProperty(SettingsConfigBase.SettingsFieldName);
            if (settings == null)
            {
                DrawDefaultInspector();
                return;
            }

            var fields = new Dictionary<string, TunableField>();
            foreach (TunableField field in TunableFields.Describe(config.SettingsType))
            {
                fields[field.Field.Name] = field;
            }

            SerializedProperty child = settings.Copy();
            SerializedProperty end = settings.GetEndProperty();
            bool enterChildren = true;
            while (child.NextVisible(enterChildren) && !SerializedProperty.EqualContents(child, end))
            {
                enterChildren = false;
                fields.TryGetValue(child.name, out TunableField field);
                DrawField(child, field);
            }

            serializedObject.ApplyModifiedProperties();
        }

        private static void DrawField(SerializedProperty property, TunableField field)
        {
            string label = property.displayName;
            string tooltip = property.tooltip;
            if (field != null)
            {
                if (field.IsTbd) label += "  [TBD]";
                string description = field.Describe();
                if (description.Length > 0) tooltip = description;
            }

            Color previous = GUI.color;
            if (field != null && field.IsPlaceholder) GUI.color = PlaceholderTint;
            EditorGUILayout.PropertyField(property, new GUIContent(label, tooltip), true);
            GUI.color = previous;
        }
    }
}
