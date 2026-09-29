using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using XRim.Config;
using XRim.Rules.Settings;

namespace XRim.Editor
{
    /// <summary>
    /// Menu XRim/Setup/Create Default Tuning Assets: creates the TuningProfile and every tuning asset with the GDD
    /// starting values (Appendix A plus marked placeholders). Safe to run again: existing assets and existing profile
    /// slots are never overwritten, only missing ones are added.
    /// </summary>
    public static class DefaultTuningAssetsCreator
    {
        [MenuItem(EditorPaths.MenuSetup + "Create Default Tuning Assets", priority = 1)]
        public static void Create()
        {
            EditorPaths.EnsureFolder(EditorPaths.TuningFolder);
            EditorPaths.EnsureFolder(EditorPaths.WeaponsFolder);
            EditorPaths.EnsureFolder(EditorPaths.BodyMovesFolder);

            var created = new List<string>();
            TuningProfile profile = LoadOrCreate<TuningProfile>(EditorPaths.TuningProfilePath, created);
            var profileObject = new SerializedObject(profile);

            Assign<MatchRulesConfig>(profileObject, TuningProfile.FieldNames.Match, "MatchRules", created);
            Assign<PathRulesConfig>(profileObject, TuningProfile.FieldNames.Paths, "PathRules", created);
            Assign<DamageConfig>(profileObject, TuningProfile.FieldNames.Damage, "Damage", created);
            Assign<HitZoneConfig>(profileObject, TuningProfile.FieldNames.HitZones, "HitZones", created);
            Assign<ClashConfig>(profileObject, TuningProfile.FieldNames.Clash, "Clash", created);
            Assign<ElectricWallConfig>(profileObject, TuningProfile.FieldNames.ElectricWall, "ElectricWall", created);
            Assign<ArenaConfig>(profileObject, TuningProfile.FieldNames.Arena, "Arena", created);
            Assign<LoadoutRulesConfig>(profileObject, TuningProfile.FieldNames.Loadout, "LoadoutRules", created);
            Assign<SignatureRulesConfig>(profileObject, TuningProfile.FieldNames.Signature, "SignatureRules", created);
            Assign<SuddenDeathConfig>(profileObject, TuningProfile.FieldNames.SuddenDeath, "SuddenDeath", created);
            Assign<SimulationConfig>(profileObject, TuningProfile.FieldNames.Simulation, "Simulation", created);
            Assign<EconomyConfig>(profileObject, TuningProfile.FieldNames.Economy, "Economy", created);
            Assign<InputConfig>(profileObject, TuningProfile.FieldNames.Input, "Input", created);
            Assign<ArenaSpaceConfig>(profileObject, TuningProfile.FieldNames.ArenaSpace, "ArenaSpace", created);
            Assign<FeelConfig>(profileObject, TuningProfile.FieldNames.Feel, "Feel", created);
            Assign<BalanceTargetsConfig>(profileObject, TuningProfile.FieldNames.BalanceTargets, "BalanceTargets", created);

            foreach (WeaponStats stats in GddStartingValues.CreateWeapons())
            {
                string path = $"{EditorPaths.WeaponsFolder}/{stats.DisplayName.Replace(" ", string.Empty)}.asset";
                AddToList(profileObject, TuningProfile.FieldNames.Weapons, LoadOrCreateSettings<WeaponDefinition, WeaponStats>(path, stats, created));
            }

            foreach (BodyMoveStats stats in GddStartingValues.CreateBodyMoves())
            {
                string path = $"{EditorPaths.BodyMovesFolder}/{stats.Move}.asset";
                AddToList(profileObject, TuningProfile.FieldNames.BodyMoves, LoadOrCreateSettings<BodyMoveDefinition, BodyMoveStats>(path, stats, created));
            }

            profileObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            Selection.activeObject = profile;
            EditorGUIUtility.PingObject(profile);

            Debug.Log(created.Count == 0
                ? "[XRim] All default tuning assets already exist; nothing was overwritten."
                : $"[XRim] Created {created.Count} tuning assets (existing assets were left untouched):\n{string.Join("\n", created)}", profile);
        }

        private static T LoadOrCreate<T>(string path, List<string> created) where T : ScriptableObject
        {
            T existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;

            T asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            created.Add(path);
            return asset;
        }

        /// <summary>Creates a settings asset seeded with specific starting values; an existing asset keeps its tuned values.</summary>
        private static TConfig LoadOrCreateSettings<TConfig, TSettings>(string path, TSettings startingValues, List<string> created)
            where TConfig : SettingsConfig<TSettings>
            where TSettings : class, new()
        {
            int countBefore = created.Count;
            TConfig config = LoadOrCreate<TConfig>(path, created);
            if (created.Count > countBefore)
            {
                config.Overwrite(startingValues);
                EditorUtility.SetDirty(config);
            }

            return config;
        }

        private static void Assign<T>(SerializedObject profile, string fieldName, string assetName, List<string> created) where T : ScriptableObject
        {
            SerializedProperty property = FindRequired(profile, fieldName);
            if (property.objectReferenceValue != null) return;
            property.objectReferenceValue = LoadOrCreate<T>($"{EditorPaths.TuningFolder}/{assetName}.asset", created);
        }

        private static void AddToList(SerializedObject profile, string fieldName, UnityEngine.Object asset)
        {
            SerializedProperty list = FindRequired(profile, fieldName);
            for (int i = 0; i < list.arraySize; i++)
            {
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == asset) return;
            }

            list.arraySize++;
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = asset;
        }

        private static SerializedProperty FindRequired(SerializedObject target, string fieldName) =>
            target.FindProperty(fieldName) ??
            throw new InvalidOperationException($"Field '{fieldName}' not found on {target.targetObject.GetType().Name}.");
    }
}
