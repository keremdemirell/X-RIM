using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using XRim.Config;
using XRim.Core;
using XRim.Economy;
using XRim.Rules.Settings;

namespace XRim.Editor
{
    /// <summary>
    /// Menu XRim/Setup/Fill New Tuning Fields. When a session adds a number to <see cref="WeaponStats"/> or
    /// <see cref="BodyMoveStats"/>, weapon and body-move assets created earlier hold 0 for it (their starting
    /// values come from <see cref="GddStartingValues"/>, not from field initializers). This fills every number that
    /// is still exactly 0 while its starting value is not. Any other value counts as tuned and is never touched.
    /// Single settings assets (Path Rules, Damage, ...) need nothing: Unity gives new fields their initializer value.
    /// The Economy asset (Session 18) is the exception: its first numbers were stored as 0 ("not decided") before the designer set the
    /// placeholder amounts, so it is filled the same way (also empty names and the shop's empty lists). A 0 there counts as unset: to
    /// test "pays nothing", set the switch off in the Economy asset after filling, not before.
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

            // The Economy asset (Session 18): its old numbers were stored as 0 before the designer set the placeholder amounts, so they are
            // filled like a weapon's. Names and the shop's lists are filled too when empty.
            foreach (EconomyConfig asset in FindAssets<EconomyConfig>())
            {
                FillAsset(asset, asset.Settings, new EconomySettings(), report, true);
            }

            AssetDatabase.SaveAssets();
            Debug.Log(report.Count == 0
                ? "[XRim] Fill New Tuning Fields: every weapon, body-move and economy asset already has all its values."
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

        private static void FillAsset(ScriptableObject asset, object current, object defaults, List<string> report, bool includeTextAndLists = false)
        {
            var changes = new List<string>();
            Undo.RecordObject(asset, UndoName);
            FillZeroFields(current, defaults, string.Empty, changes, includeTextAndLists);
            if (changes.Count == 0) return;
            EditorUtility.SetDirty(asset);
            foreach (string change in changes)
            {
                report.Add($"{AssetDatabase.GetAssetPath(asset)}: {change}");
            }
        }

        private static void FillZeroFields(object current, object defaults, string prefix, List<string> changes, bool includeTextAndLists)
        {
            foreach (FieldInfo field in current.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                object now = field.GetValue(current);
                object start = field.GetValue(defaults);
                string name = prefix + field.Name;
                bool fillable = IsNumeric(field.FieldType) || (includeTextAndLists && IsTextOrList(field.FieldType));
                if (fillable && IsUnset(now, field.FieldType) && !IsUnset(start, field.FieldType))
                {
                    field.SetValue(current, start);
                    changes.Add($"{name} = {Describe(start)}");
                }
                else if (IsNestedSettings(field.FieldType) && now != null && start != null)
                {
                    FillZeroFields(now, start, name + ".", changes, includeTextAndLists);
                }
            }
        }

        private static bool IsNumeric(Type type) => type == typeof(float) || type == typeof(int) || type == typeof(long) || type == typeof(Vec2);

        private static bool IsTextOrList(Type type) =>
            type == typeof(string) || (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>));

        private static bool IsUnset(object value, Type type) =>
            type == typeof(float) ? (float)value == 0f
            : type == typeof(int) ? (int)value == 0
            : type == typeof(long) ? (long)value == 0L
            : type == typeof(string) ? string.IsNullOrEmpty((string)value)
            : type == typeof(Vec2) ? (Vec2)value == Vec2.Zero
            : value == null || (value is System.Collections.IList list && list.Count == 0);

        private static string Describe(object value) =>
            value is System.Collections.IList list ? $"{list.Count} item(s)" : value.ToString();

        private static bool IsNestedSettings(Type type) =>
            type.IsClass && type != typeof(string) && !typeof(System.Collections.IEnumerable).IsAssignableFrom(type) &&
            type.IsDefined(typeof(SerializableAttribute), false);
    }
}
