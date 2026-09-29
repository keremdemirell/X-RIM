using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using XRim.Config;
using XRim.Core;
using XRim.Rules.Settings;

namespace XRim.Editor
{
    /// <summary>
    /// Menu XRim/Setup/Fill New Tuning Fields. When a session adds a number to <see cref="WeaponStats"/> or
    /// <see cref="BodyMoveStats"/>, weapon and body-move assets created earlier hold 0 for it (their starting
    /// values come from <see cref="GddStartingValues"/>, not from field initializers). This fills every number that
    /// is still exactly 0 while its starting value is not. Any other value counts as tuned and is never touched.
    /// Single settings assets (Path Rules, Damage, ...) need nothing: Unity gives new fields their initializer value.
    /// </summary>
    public static class NewTuningFieldsFiller
    {
        private const string UndoName = "Fill New Tuning Fields";

        [MenuItem(EditorPaths.MenuSetup + "Fill New Tuning Fields", priority = 4)]
        public static void Fill()
        {
            var report = new List<string>();
            List<WeaponStats> weaponDefaults = GddStartingValues.CreateWeapons();
            foreach (WeaponDefinition asset in FindAssets<WeaponDefinition>())
            {
                WeaponStats defaults = weaponDefaults.Find(stats => stats.Id == asset.Settings.Id);
                if (defaults != null) FillAsset(asset, asset.Settings, defaults, report);
            }

            List<BodyMoveStats> moveDefaults = GddStartingValues.CreateBodyMoves();
            foreach (BodyMoveDefinition asset in FindAssets<BodyMoveDefinition>())
            {
                BodyMoveStats defaults = moveDefaults.Find(stats => stats.Move == asset.Settings.Move);
                if (defaults != null) FillAsset(asset, asset.Settings, defaults, report);
            }

            AssetDatabase.SaveAssets();
            Debug.Log(report.Count == 0
                ? "[XRim] Fill New Tuning Fields: every weapon and body-move asset already has all its values."
                : $"[XRim] Fill New Tuning Fields: filled {report.Count} value(s) from the GDD starting values:\n{string.Join("\n", report)}");
        }

        private static IEnumerable<T> FindAssets<T>() where T : ScriptableObject
        {
            foreach (string guid in AssetDatabase.FindAssets("t:" + typeof(T).Name))
            {
                T asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset != null) yield return asset;
            }
        }

        private static void FillAsset(ScriptableObject asset, object current, object defaults, List<string> report)
        {
            var changes = new List<string>();
            Undo.RecordObject(asset, UndoName);
            FillZeroFields(current, defaults, string.Empty, changes);
            if (changes.Count == 0) return;
            EditorUtility.SetDirty(asset);
            foreach (string change in changes)
            {
                report.Add($"{AssetDatabase.GetAssetPath(asset)}: {change}");
            }
        }

        private static void FillZeroFields(object current, object defaults, string prefix, List<string> changes)
        {
            foreach (FieldInfo field in current.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                object now = field.GetValue(current);
                object start = field.GetValue(defaults);
                string name = prefix + field.Name;
                if (IsUnset(now, field.FieldType) && !IsUnset(start, field.FieldType) && IsNumeric(field.FieldType))
                {
                    field.SetValue(current, start);
                    changes.Add($"{name} = {start}");
                }
                else if (IsNestedSettings(field.FieldType) && now != null && start != null)
                {
                    FillZeroFields(now, start, name + ".", changes);
                }
            }
        }

        private static bool IsNumeric(Type type) => type == typeof(float) || type == typeof(int) || type == typeof(Vec2);

        private static bool IsUnset(object value, Type type) =>
            type == typeof(float) ? (float)value == 0f
            : type == typeof(int) ? (int)value == 0
            : type == typeof(Vec2) && (Vec2)value == Vec2.Zero;

        private static bool IsNestedSettings(Type type) =>
            type.IsClass && type != typeof(string) && !typeof(System.Collections.IEnumerable).IsAssignableFrom(type) &&
            type.IsDefined(typeof(SerializableAttribute), false);
    }
}
