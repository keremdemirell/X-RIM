using UnityEditor;

namespace XRim.Editor
{
    /// <summary>Menu paths and asset locations used by the XRim Editor tools.</summary>
    internal static class EditorPaths
    {
        public const string MenuSetup = "XRim/Setup/";
        public const string MenuReports = "XRim/Reports/";
        public const string MenuSpike = "XRim/Spike/";

        public const string XRimRoot = "Assets/XRim";
        public const string DataFolder = XRimRoot + "/Data";
        public const string TuningFolder = DataFolder + "/Tuning";
        public const string WeaponsFolder = DataFolder + "/Weapons";
        public const string BodyMovesFolder = DataFolder + "/BodyMoves";
        public const string ScenesFolder = XRimRoot + "/Scenes";
        public const string PlaceholderArtFolder = XRimRoot + "/Art/Placeholder";
        public const string SpikePrefabsFolder = XRimRoot + "/Prefabs/Spike";

        public const string TuningProfilePath = TuningFolder + "/TuningProfile.asset";
        public const string SandboxScenePath = ScenesFolder + "/Sandbox.unity";
        public const string SpikeScenePath = ScenesFolder + "/Spike.unity";

        /// <summary>Creates every missing folder along an "Assets/..." path.</summary>
        public static void EnsureFolder(string path)
        {
            string[] parts = path.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
